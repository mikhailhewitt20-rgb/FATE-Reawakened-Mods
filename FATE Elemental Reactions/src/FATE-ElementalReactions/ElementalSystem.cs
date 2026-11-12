using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HarmonyLib;
using Tableflip;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace FateElementalReactions;

internal enum CombatElement { None, Pyro, Cryo, Electro, Geo, Anemo, Hydro }

public sealed partial class Plugin
{
    private const float ElementAuraSeconds = 4f;
    private const float ReactionAuraSeconds = 1.4f;
    private const float ReactionLockSeconds = 1.2f;
    private static readonly (string Gem, string ElementName, string Label, DamageType DamageType, CombatElement Element)[] ExistingElementGems =
    {
        ("Ruby", "Fire", "Pyro", DamageType.Fire, CombatElement.Pyro),
        ("Opal", "Ice", "Cryo", DamageType.Ice, CombatElement.Cryo),
        ("Turquoise", "Electrical", "Electro", DamageType.Electric, CombatElement.Electro)
    };
    private static readonly (string Gem, string ElementName, string SourceGem, CombatElement Element, float Hue, Color Tint)[] NewElementGems =
    {
        ("Citrine", "Geo", "Turquoise", CombatElement.Geo, .13f, new Color(1f, .77f, .08f)),
        ("Cyanite", "Anemo", "Turquoise", CombatElement.Anemo, .49f, new Color(.10f, .95f, .93f)),
        ("Aquamarine", "Hydro", "Opal", CombatElement.Hydro, .60f, new Color(.06f, .20f, .95f))
    };
    private sealed class Aura
    {
        internal CombatElement Element;
        internal float AppliedAt;
        internal float ExpiresAt;
    }

    private readonly Dictionary<CharacterVitals, List<Aura>> elementalAuras = new Dictionary<CharacterVitals, List<Aura>>();
    private readonly Dictionary<CharacterVitals, float> reactionLocks = new Dictionary<CharacterVitals, float>();
    private readonly HashSet<CharacterVitals> applyingReactionDamage = new HashSet<CharacterVitals>();
    private readonly Dictionary<Character, ElementIndicator> elementalIndicators = new Dictionary<Character, ElementIndicator>();
    private readonly HashSet<Monster> trainingDummies = new HashSet<Monster>();
    private readonly Dictionary<Monster, Vector3> trainingDummyPositions = new Dictionary<Monster, Vector3>();
    private readonly List<ItemData> addedElementGemVariants = new List<ItemData>();
    private float nextElementalTestPoll, nextElementalIndicatorUpdate, nextAutomaticDummyAttempt;
    private int dummyIndex;
    private bool automaticDummiesSpawned;

    private sealed class ElementIndicator
    {
        internal GameObject root = null!;
        internal SpriteRenderer[] images = Array.Empty<SpriteRenderer>();
    }

    private void PrepareElementalGems(FateDB db)
    {
        var updated = 0;
        foreach (var definition in ExistingElementGems)
        {
            var gems = db.AllItems.Where(item => item != null && item.itemType == ItemType.Gem &&
                ((item.refId ?? "").IndexOf(definition.Gem, StringComparison.OrdinalIgnoreCase) >= 0 ||
                 (item.displayName ?? "").IndexOf(definition.Gem, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
            foreach (var gem in gems)
            {
                var effects = gem.itemEffects ?? new List<EffectData>();
                var damageEffect = effects.FirstOrDefault(effect => effect != null && effect.buffType == EffectType.AddDamageType);
                if (damageEffect == null)
                {
                    var amount = gem.damageBonuses?.Sum(bonus => bonus.value) ?? 0;
                    if (amount <= 0) amount = 6;
                    damageEffect = new EffectData { buffType = EffectType.AddDamageType, name = definition.DamageType.ToString(), value = amount };
                    effects.Add(damageEffect);
                    gem.itemEffects = effects;
                }
                else damageEffect.name = definition.DamageType.ToString();

                if (gem.damageBonuses != null)
                    foreach (var bonus in gem.damageBonuses) bonus.type = definition.DamageType;

                var bonusValue = Mathf.Max(0, damageEffect.value);
                gem.description = $"Adds +{bonusValue} {definition.Label} ({definition.ElementName}) damage to a weapon when installed. Adds +{bonusValue * 4} {definition.Label} ({definition.ElementName}) resistance to armor when installed.";
                updated++;
            }
        }
        AddNewElementGemVariants(db);
        Logger.LogInfo($"ELEMENT_GEMS_READY configured {updated} existing Ruby, Opal and Turquoise tiers and added {addedElementGemVariants.Count} Geo, Anemo and Hydro gem variants with custom inventory icons.");
    }

    private void AddNewElementGemVariants(FateDB db)
    {
        addedElementGemVariants.Clear();
        var sourceItems = db.AllItems.Where(item => item != null && item.itemType == ItemType.Gem &&
            ExistingElementGems.Any(element => HasGemName(item, element.Gem))).ToList();

        foreach (var definition in NewElementGems)
        {
            var preferredSources = sourceItems.Where(item => HasGemName(item, definition.SourceGem))
                .OrderBy(item => item.minimumDepth).ThenBy(item => item.rarity).ThenBy(item => item.maximumDepth).ToList();
            var templates = new List<ItemData>();
            foreach (var candidate in preferredSources.Concat(sourceItems))
            {
                if (!templates.Any(existing => SameGemTier(existing, candidate))) templates.Add(candidate);
            }

            foreach (var template in templates)
            {
                var prefix = GemVariantPrefix(template, definition.SourceGem);
                var displayName = prefix + definition.Gem;
                var tierSlug = Regex.Replace(prefix.Trim(), "[^A-Za-z0-9]+", "_").Trim('_');
                var tierCode = $"R{template.rarity}_D{template.minimumDepth}_{template.maximumDepth}_V{template.value}";
                var refId = "ZG_Elemental_" + definition.Element + "_" + (tierSlug.Length == 0 ? "" : tierSlug + "_") + tierCode + "_" + definition.Gem;
                var existing = db.AllItems.FirstOrDefault(item => item != null && string.Equals(item.refId, refId, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    addedElementGemVariants.Add(existing);
                    continue;
                }

                var clone = Object.Instantiate(template);
                clone.name = refId;
                clone.refId = refId;
                clone.displayName = displayName;
                clone.iconPath = "ICONS/" + refId.ToLowerInvariant() + ".png:ICONS/" + refId.ToLowerInvariant() + "a.png";
                clone.description = "";
                clone.reserved = false;
                clone.notPurchasable = true;
                clone.notSellable = false;
                AccessTools.Field(typeof(ItemData), "iconName")?.SetValue(clone, null);
                clone.damageBonuses = (template.damageBonuses ?? new List<ElementAmount>())
                    .Select(bonus => new ElementAmount { type = DamageType.Magic, value = bonus.value }).ToList();
                clone.itemEffects = (template.itemEffects ?? new List<EffectData>()).Select(effect => new EffectData(effect)).ToList();
                foreach (var effect in clone.itemEffects)
                {
                    if (effect == null) continue;
                    if (effect.buffType == EffectType.AddDamageType)
                    {
                        effect.name = DamageType.Magic.ToString();
                        effect.damageType = DamageType.Magic;
                    }
                }
                var flatDamage = clone.damageBonuses.Sum(bonus => bonus.value);
                if (flatDamage <= 0)
                    flatDamage = clone.itemEffects.FirstOrDefault(effect => effect != null && effect.buffType == EffectType.AddDamageType)?.value ?? 6;
                clone.description = $"Adds +{flatDamage} {definition.ElementName} Damage when installed in a weapon. Adds +{flatDamage * 4} {definition.ElementName} Resistance when installed in armor.";
                db.AllItems.Add(clone);
                addedElementGemVariants.Add(clone);
                AddNewGemInventoryIcon(db, template, clone, definition.Hue);
            }
        }
    }

    private static bool HasGemName(ItemData item, string gemName)
    {
        var name = string.Join(" ", item.refId ?? "", item.displayName ?? "");
        return name.IndexOf(gemName, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool SameGemTier(ItemData left, ItemData right) =>
        left.rarity == right.rarity && left.minimumDepth == right.minimumDepth &&
        left.maximumDepth == right.maximumDepth;

    private static string GemVariantPrefix(ItemData template, string sourceGem)
    {
        var name = !string.IsNullOrWhiteSpace(template.displayName) ? template.displayName! : template.refId ?? "";
        name = name.Replace("Item_", "", StringComparison.OrdinalIgnoreCase);
        var matchingSource = ExistingElementGems.Select(element => element.Gem)
            .OrderByDescending(gem => gem.Length)
            .FirstOrDefault(gem => name.IndexOf(gem, StringComparison.OrdinalIgnoreCase) >= 0) ?? sourceGem;
        var gemIndex = name.IndexOf(matchingSource, StringComparison.OrdinalIgnoreCase);
        return gemIndex >= 0 ? name.Substring(0, gemIndex).Trim() + (gemIndex > 0 ? " " : "") : "";
    }

    private static void AddNewGemInventoryIcon(FateDB db, ItemData template, ItemData clone, float hue)
    {
        try
        {
            var sourceIcon = db.spriteStash?.FirstOrDefault(sprite => sprite != null &&
                string.Equals(sprite.name, template.IconName, StringComparison.OrdinalIgnoreCase));
            if (sourceIcon == null) return;
            var area = sourceIcon.textureRect;
            var previous = RenderTexture.active;
            RenderTexture buffer = null!;
            Texture2D texture = null!;
            try
            {
                buffer = RenderTexture.GetTemporary(sourceIcon.texture.width, sourceIcon.texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                Graphics.Blit(sourceIcon.texture, buffer);
                RenderTexture.active = buffer;
                texture = new Texture2D((int)area.width, (int)area.height, TextureFormat.RGBA32, false) { name = clone.refId!.ToLowerInvariant() };
                texture.ReadPixels(area, 0, 0);
                var pixels = texture.GetPixels32();
                for (var i = 0; i < pixels.Length; i++)
                {
                    var color = (Color)pixels[i];
                    if (color.a <= 0) continue;
                    Color.RGBToHSV(color, out _, out var saturation, out var value);
                    var tinted = Color.HSVToRGB(hue, Mathf.Max(.15f, saturation), value);
                    tinted.a = color.a;
                    pixels[i] = tinted;
                }
                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                var icon = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), sourceIcon.pixelsPerUnit);
                icon.name = clone.IconName;
                db.spriteStash = (db.spriteStash ?? Array.Empty<Sprite>()).Append(icon).ToArray();
                texture = null!;
            }
            finally
            {
                RenderTexture.active = previous;
                if (buffer) RenderTexture.ReleaseTemporary(buffer);
                if (texture) Object.Destroy(texture);
            }
        }
        catch (Exception e) { Debug.LogWarning($"[FATE Elemental Reactions] Could not recolor inventory icon for {clone.displayName}: {e.Message}"); }
    }

    private static CombatElement ElementFromWeapon(Character? attacker, DamageType damageType)
    {
        if (damageType == DamageType.Fire) return CombatElement.Pyro;
        if (damageType == DamageType.Ice) return CombatElement.Cryo;
        if (damageType == DamageType.Electric) return CombatElement.Electro;
        var weapon = attacker?.inventory?.FirstOrDefault(item => item != null && item.equipped && item.IsInMainHand);
        foreach (var gem in weapon?.sockedtedItems ?? Enumerable.Empty<Item>())
        {
            if (gem == null) continue;
            var name = string.Join(" ", gem.itemData?.refId ?? "", gem.itemData?.displayName ?? "", gem.itemBaseName ?? "");
            if (name.IndexOf("Ruby", StringComparison.OrdinalIgnoreCase) >= 0) return CombatElement.Pyro;
            if (name.IndexOf("Turquoise", StringComparison.OrdinalIgnoreCase) >= 0) return CombatElement.Electro;
            if (name.IndexOf("Opal", StringComparison.OrdinalIgnoreCase) >= 0) return CombatElement.Cryo;
            if (name.IndexOf("Citrine", StringComparison.OrdinalIgnoreCase) >= 0) return CombatElement.Geo;
            if (name.IndexOf("Cyanite", StringComparison.OrdinalIgnoreCase) >= 0) return CombatElement.Anemo;
            if (name.IndexOf("Aquamarine", StringComparison.OrdinalIgnoreCase) >= 0) return CombatElement.Hydro;
        }
        return CombatElement.None;
    }

    private static CombatElement InnateElement(Character? character)
    {
        if (!(character is Monster monster) || monster.monsterData == null) return CombatElement.None;
        var name = string.Join(" ", monster.monsterData.refId ?? "", monster.monsterData.displayName ?? "", monster.CharacterName ?? "");
        if (ContainsAny(name, "fire", "flame", "pyro")) return CombatElement.Pyro;
        if (ContainsAny(name, "ice", "frost", "cryo")) return CombatElement.Cryo;
        if (ContainsAny(name, "electric", "lightning", "shock", "electro")) return CombatElement.Electro;
        if (ContainsAny(name, "earth", "stone", "geo")) return CombatElement.Geo;
        if (ContainsAny(name, "wind", "air", "anemo")) return CombatElement.Anemo;
        if (ContainsAny(name, "water", "hydro")) return CombatElement.Hydro;
        return CombatElement.None;
    }

    private static bool ContainsAny(string value, params string[] fragments) =>
        fragments.Any(fragment => value.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0);

    private static void BeforeElementalDamage(CharacterVitals __instance, ref CharacterVitals.Damage data)
    {
        var plugin = Active;
        if (ReferenceEquals(plugin, null) || data == null) return;
        var attacker = data.source?.Char;
        var element = ElementFromWeapon(attacker, data.damageType);
        SetNativeDamagePopupTint(DamageTint(element, data.damageType));
        if (data.value <= 0 || __instance.Char == null || plugin.applyingReactionDamage.Contains(__instance)) return;
        if (element == CombatElement.None) return;
        plugin.ApplyElementalAura(__instance, attacker, element, ref data);
    }

    private static void SetNativeDamagePopupTint(Color tint)
    {
        var type = AccessTools.TypeByName("Cleverous.ThatHurtModule.ThatHurt");
        var instance = type == null ? null : AccessTools.Field(type, "Instance")?.GetValue(null);
        var colorField = type == null ? null : AccessTools.Field(type, "damageColor");
        if (instance != null && colorField != null) colorField.SetValue(instance, tint);
    }

    private void ApplyElementalAura(CharacterVitals target, Character? attacker, CombatElement incoming, ref CharacterVitals.Damage data)
    {
        var now = Time.unscaledTime;
        var active = GetActiveAuras(target);
        var current = active.LastOrDefault(aura => aura.Element != incoming);
        var innate = InnateElement(target.Char);
        var previous = current?.Element ?? (innate != incoming ? innate : CombatElement.None);
        var reaction = previous == CombatElement.None || reactionLocks.TryGetValue(target, out var lockedUntil) && lockedUntil > now
            ? "" : ReactionName(previous, incoming);

        if (reaction.Length > 0)
        {
            if (reaction == "Melt")
                data.value = Mathf.Max(1, Mathf.RoundToInt(data.value * (incoming == CombatElement.Pyro ? 2f : 1.5f)));
            Logger.LogInfo($"ELEMENT_REACTION {reaction} {previous}+{incoming} damage={data.value}");
            ShowFloatingCombatText(target.Char.transform.position + Vector3.up * 3.4f,
                reaction.ToUpperInvariant(), ReactionTint(previous, incoming), 1.45f, .32f);
            ApplyElementalReaction(reaction, target, attacker, data.value);

            foreach (var aura in active) aura.ExpiresAt = now + ReactionAuraSeconds;
            if (!active.Any(aura => aura.Element == incoming))
                active.Add(new Aura { Element = incoming, AppliedAt = now, ExpiresAt = now + ReactionAuraSeconds });
            TrimAuras(active);
            elementalAuras[target] = active;
            reactionLocks[target] = now + ReactionLockSeconds;
            return;
        }

        var same = active.FindIndex(aura => aura.Element == incoming);
        if (same >= 0)
        {
            active[same].AppliedAt = now;
            active[same].ExpiresAt = now + ElementAuraSeconds;
        }
        else
        {
            active.Add(new Aura { Element = incoming, AppliedAt = now, ExpiresAt = now + ElementAuraSeconds });
            TrimAuras(active);
        }
        elementalAuras[target] = active;
    }

    private List<Aura> GetActiveAuras(CharacterVitals target)
    {
        if (!elementalAuras.TryGetValue(target, out var active)) return new List<Aura>();
        active.RemoveAll(aura => aura.ExpiresAt <= Time.unscaledTime);
        if (active.Count == 0) elementalAuras.Remove(target);
        return active;
    }

    private static void TrimAuras(List<Aura> auras)
    {
        while (auras.Count > 2) auras.Remove(auras.OrderBy(aura => aura.AppliedAt).First());
    }

    private static string ReactionName(CombatElement first, CombatElement second)
    {
        if ((first == CombatElement.Pyro && second == CombatElement.Cryo) || (first == CombatElement.Cryo && second == CombatElement.Pyro)) return "Melt";
        if ((first == CombatElement.Pyro && second == CombatElement.Electro) || (first == CombatElement.Electro && second == CombatElement.Pyro)) return "Overloaded";
        if ((first == CombatElement.Cryo && second == CombatElement.Electro) || (first == CombatElement.Electro && second == CombatElement.Cryo)) return "Superconduct";
        return "";
    }

    private void ApplyElementalReaction(string reaction, CharacterVitals target, Character? attacker, int hitDamage)
    {
        if (attacker == null) return;
        var source = attacker.MyVitals;
        var amount = Mathf.Max(1, hitDamage);
        if (reaction == "Overloaded")
            AreaReactionDamage(target, source, Mathf.Max(1, Mathf.RoundToInt(amount * .35f)), 3.25f);
        else if (reaction == "Superconduct")
            ApplySuperconduct(target.Char, source);
    }

    private void AreaReactionDamage(CharacterVitals center, CharacterVitals source, int damage, float radius)
    {
        var origin = center.Char.transform.position;
        foreach (var monster in Object.FindObjectsOfType<Monster>())
        {
            if (monster == null || monster.MyVitals == center || monster.MyVitals.IsDead ||
                monster.GetAlignment() == source.Alignment || trainingDummies.Contains(monster) ||
                (monster.transform.position - origin).sqrMagnitude > radius * radius) continue;
            applyingReactionDamage.Add(monster.MyVitals);
            try { monster.MyVitals.ApplyDamage(new CharacterVitals.Damage { value = damage, damageType = DamageType.Fire, source = source }); }
            catch (Exception e) { Logger.LogDebug("Overloaded splash failed: " + e.Message); }
            finally { applyingReactionDamage.Remove(monster.MyVitals); }
        }
    }

    private static void ApplySuperconduct(Character target, CharacterVitals source)
    {
        AddTimedEffect(target, source, EffectType.PercentCrushingResistance, -20, "ZG_Superconduct_Crush");
        AddTimedEffect(target, source, EffectType.PercentSlashingResistance, -20, "ZG_Superconduct_Slash");
        AddTimedEffect(target, source, EffectType.PercentPiercingResistance, -20, "ZG_Superconduct_Pierce");
    }

    private static void AddTimedEffect(Character target, CharacterVitals source, EffectType type, int value, string name)
    {
        var effect = new EffectData
        {
            name = name, buffType = type, value = value, duration = 5 * 60,
            activationType = ActivationType.Passive, target = ItemTarget.Character, exclusive = true, chance = 100
        };
        target.AddEffect(effect, 1, source.Char, -1, false);
    }

    private static bool BeforeTrainingDummyRespond(Monster __instance)
    {
        var plugin = Active;
        return ReferenceEquals(plugin, null) || !plugin.trainingDummies.Contains(__instance);
    }

    private static bool BeforeTrainingDummyAI(Character __instance, AIState newState) =>
        !(__instance is Monster dummy && IsTrainingDummy(dummy)) || newState == AIState.Inactive;

    private static bool IsTrainingDummy(Monster dummy)
    {
        var plugin = Active;
        return !ReferenceEquals(plugin, null) && plugin.trainingDummies.Contains(dummy);
    }

    private static bool BeforeTrainingDummyDie(CharacterVitals __instance)
    {
        var plugin = Active;
        return ReferenceEquals(plugin, null) || !(__instance.Char is Monster dummy) || !plugin.trainingDummies.Contains(dummy);
    }
    private static bool BeforeTrainingDummyDieQuiet(CharacterVitals __instance) => BeforeTrainingDummyDie(__instance);

    private static void AfterElementalDamage(CharacterVitals __instance, CharacterVitals.Damage data)
    {
        var plugin = Active;
        if (ReferenceEquals(plugin, null) || data == null) return;
        if (__instance.Char is Monster dummy && plugin.trainingDummies.Contains(dummy))
        {
            dummy.hp = Math.Max(dummy.hp, 1000000f);
            __instance.OnResourcesUpdated();
        }
    }

    private static Color DamageTint(CombatElement element, DamageType damageType)
    {
        if (element != CombatElement.None) return ElementTint(element);
        return damageType switch
        {
            DamageType.Magic => new Color(.8f, .55f, 1f),
            DamageType.Poison => new Color(.55f, 1f, .35f),
            DamageType.Undead => new Color(.72f, .82f, .84f),
            DamageType.Fire => ElementTint(CombatElement.Pyro),
            DamageType.Ice => ElementTint(CombatElement.Cryo),
            DamageType.Electric => ElementTint(CombatElement.Electro),
            // Physical and other untyped damage remain neutral; reserve yellow for Geo.
            _ => Color.white
        };
    }

    private void TickElementalEffects()
    {
        var now = Time.unscaledTime;
        foreach (var target in elementalAuras.Keys.Where(target => target == null).ToList()) elementalAuras.Remove(target);
        foreach (var target in elementalAuras.Keys.ToList())
        {
            var auras = elementalAuras[target];
            auras.RemoveAll(aura => aura.ExpiresAt <= now);
            if (auras.Count == 0) elementalAuras.Remove(target);
        }
        foreach (var target in reactionLocks.Where(pair => pair.Key == null || pair.Value <= now).Select(pair => pair.Key).ToList())
            reactionLocks.Remove(target);
    }

    private void UpdateElementalIndicators()
    {
        if (Time.unscaledTime < nextElementalIndicatorUpdate) return;
        nextElementalIndicatorUpdate = Time.unscaledTime + .2f;
        var actors = new HashSet<Character>();
        if (Player.PlayerChar != null) actors.Add(Player.PlayerChar);
        foreach (var pair in elementalAuras)
            if (pair.Key != null && pair.Key.Char != null) actors.Add(pair.Key.Char);
        foreach (var monster in Object.FindObjectsOfType<Monster>())
            if (monster != null && monster.gameObject.activeInHierarchy && InnateElement(monster) != CombatElement.None) actors.Add(monster);
        var db = SingletonBehaviour<FateDB>.Instance;
        foreach (var actor in actors)
        {
            if (actor == null || actor.MyVitals == null || actor.MyVitals.IsDead)
            {
                if (actor != null && elementalIndicators.TryGetValue(actor, out var dead) && dead.root) dead.root.SetActive(false);
                continue;
            }

            var elements = new List<CombatElement>();
            var innate = InnateElement(actor);
            if (innate != CombatElement.None) elements.Add(innate);
            if (elementalAuras.TryGetValue(actor.MyVitals, out var active))
                foreach (var aura in active.Where(aura => aura.ExpiresAt > Time.unscaledTime).OrderBy(aura => aura.AppliedAt))
                    if (!elements.Contains(aura.Element)) elements.Add(aura.Element);
            if (elements.Count == 0)
            {
                if (elementalIndicators.TryGetValue(actor, out var inactive) && inactive.root) inactive.root.SetActive(false);
                continue;
            }

            if (!elementalIndicators.TryGetValue(actor, out var indicator) || !indicator.root) indicator = CreateElementIndicator(actor);
            indicator.root.transform.localPosition = Vector3.up * (actor is Monster ? 2.6f : 3.55f);
            indicator.root.transform.localScale = Vector3.one * .62f;
            var visibleCount = Math.Min(elements.Count, indicator.images.Length);
            for (var i = 0; i < indicator.images.Length; i++)
            {
                var image = indicator.images[i];
                if (i >= visibleCount) { image.gameObject.SetActive(false); continue; }
                var itemName = elements[i] switch { CombatElement.Pyro => "ruby", CombatElement.Electro => "turquoise", CombatElement.Cryo => "opal", _ => "" };
                image.sprite = db?.spriteStash?.FirstOrDefault(sprite => sprite != null && string.Equals(sprite.name, itemName, StringComparison.OrdinalIgnoreCase));
                image.color = Color.white;
                image.transform.localPosition = new Vector3((i - (visibleCount - 1) / 2f) * .72f, 0, 0);
                image.gameObject.SetActive(image.sprite != null);
            }
            indicator.root.SetActive(true);
        }
        foreach (var stale in elementalIndicators.Keys.Where(actor => actor == null || !actors.Contains(actor)).ToList())
        {
            if (elementalIndicators[stale].root) Object.Destroy(elementalIndicators[stale].root);
            elementalIndicators.Remove(stale);
        }
    }

    private ElementIndicator CreateElementIndicator(Character actor)
    {
        var root = new GameObject("ZG_Element_Indicator");
        root.transform.SetParent(actor.transform, false);
        root.AddComponent<Billboard>();
        var images = new SpriteRenderer[3];
        for (var i = 0; i < images.Length; i++)
        {
            var icon = new GameObject("Element_" + i);
            icon.transform.SetParent(root.transform, false);
            var renderer = icon.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 20;
            images[i] = renderer;
        }
        var indicator = new ElementIndicator { root = root, images = images };
        elementalIndicators[actor] = indicator;
        return indicator;
    }

    private static Color ElementTint(CombatElement element) => element switch
    {
        CombatElement.Pyro => new Color(1f, .28f, .12f), CombatElement.Electro => new Color(.72f, .35f, 1f),
        CombatElement.Cryo => new Color(.55f, .88f, 1f), CombatElement.Geo => new Color(1f, .77f, .08f),
        CombatElement.Anemo => new Color(.10f, .95f, .93f), CombatElement.Hydro => new Color(.06f, .20f, .95f),
        _ => Color.white
    };

    private bool GrantNewElementGemVariantsToCharacter()
    {
        var player = Player.PlayerChar ?? throw new InvalidOperationException("Load a character before granting the new elemental gems.");
        if (addedElementGemVariants.Count == 0) throw new InvalidOperationException("The new elemental gem variants have not been added to FATE's item database yet.");
        var granted = 0;
        var inventoryFull = false;
        foreach (var gemData in addedElementGemVariants)
        {
            if (gemData == null || player.inventory.Any(item => item != null &&
                string.Equals(item.itemData?.refId, gemData.refId, StringComparison.OrdinalIgnoreCase))) continue;
            var gem = new Item(gemData);
            gem.SetIdentified(true);
            if (player.AddItemToInventory(gem, AddItemReason.Loot)) granted++;
            else inventoryFull = true;
        }
        if (inventoryFull)
        {
            Logger.LogWarning($"Granted {granted} new elemental gems to {player.CharacterName}; inventory space is needed for the remaining variants.");
            return false;
        }
        Logger.LogInfo($"ELEMENT_NEW_GEMS_GRANTED added {granted} Geo, Anemo and Hydro gem variants to {player.CharacterName}; existing copies were skipped.");
        return true;
    }

    private static Color ReactionTint(CombatElement first, CombatElement second) => Color.Lerp(ElementTint(first), ElementTint(second), .5f);

    private sealed class Billboard : MonoBehaviour
    {
        private void LateUpdate() { if (Camera.main != null) transform.rotation = Camera.main.transform.rotation; }
    }

    private void ShowFloatingCombatText(Vector3 position, string value, Color tint, float lifetime, float rise)
    {
        var root = new GameObject("ZG_Elemental_CombatText");
        root.transform.position = position;
        var text = root.AddComponent<TextMesh>();
        text.text = value;
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = 48;
        text.characterSize = .065f;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.fontStyle = FontStyle.Bold;
        text.color = tint;
        var renderer = root.GetComponent<MeshRenderer>();
        if (renderer != null) renderer.sortingOrder = 300;
        root.AddComponent<Billboard>();
        root.AddComponent<FloatingCombatText>().Initialize(text, tint, lifetime, rise);
    }

    private sealed class FloatingCombatText : MonoBehaviour
    {
        private TextMesh? label;
        private Color baseColor;
        private float duration, rise, elapsed;
        private Vector3 origin;

        internal void Initialize(TextMesh text, Color color, float lifetime, float riseDistance)
        {
            label = text;
            baseColor = color;
            duration = lifetime;
            rise = riseDistance;
            origin = transform.position;
        }

        private void Update()
        {
            if (label == null) { Object.Destroy(gameObject); return; }
            elapsed += Time.unscaledDeltaTime;
            var progress = Mathf.Clamp01(elapsed / duration);
            transform.position = origin + Vector3.up * (rise * progress);
            var color = baseColor;
            color.a = 1f - progress;
            label.color = color;
            if (progress >= 1f) Object.Destroy(gameObject);
        }
    }

    private void UpdateElementalTesting()
    {
        TickElementalEffects();
        var inGrove = SceneLoad.CurrentScene == Tableflip.Scene.Town_Grove;
        if (!inGrove) automaticDummiesSpawned = false;
        else if (autoSpawnDummies?.Value == true && !automaticDummiesSpawned && Player.PlayerChar != null && Time.unscaledTime >= nextAutomaticDummyAttempt)
        {
            nextAutomaticDummyAttempt = Time.unscaledTime + 2f;
            try
            {
                SpawnElementalTrainingDummies();
                automaticDummiesSpawned = trainingDummies.Count > 0;
            }
            catch (Exception e) { Logger.LogDebug("Automatic Grove dummy spawn is waiting: " + e.Message); }
        }

        if (TestCommandsEnabled && Keyboard.current?.f8Key.wasPressedThisFrame == true)
        {
            Logger.LogInfo($"ELEMENT_HOTKEY F8 pressed; scene={SceneLoad.CurrentScene}, characterLoaded={Player.PlayerChar != null}.");
            if (SceneLoad.CurrentScene != Tableflip.Scene.Town_Grove || Player.PlayerChar == null)
                Logger.LogWarning("F8 dummy spawn needs an existing character loaded in Grove.");
            else
            {
                try { SpawnElementalTrainingDummies(); }
                catch (Exception e) { Logger.LogError("F8 dummy spawn failed: " + e); }
            }
        }
        if (!TestCommandsEnabled || Time.unscaledTime < nextElementalTestPoll) return;
        nextElementalTestPoll = Time.unscaledTime + .2f;
        var commandPath = CommandDirectory;
        Directory.CreateDirectory(commandPath);
        if (!Directory.Exists(commandPath)) return;
        foreach (var file in Directory.GetFiles(commandPath, "*.cmd").OrderBy(path => path, StringComparer.Ordinal))
        {
            var body = File.ReadAllText(file).Trim();
            var parts = body.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0 && parts[0] == "test-elemental-new-gems" &&
                (Player.PlayerChar == null || !contentPrepared || addedElementGemVariants.Count == 0))
                continue;
            var deferCommand = false;
            try
            {
                if (parts.Length == 0) continue;
                if (parts[0] == "test-elemental-new-gems") deferCommand = !GrantNewElementGemVariantsToCharacter();
                else if (parts[0] == "test-elemental-dummies") SpawnElementalTrainingDummies();
                else if (parts[0] == "test-elemental-weapons" || parts[0] == "test-elemental-kit")
                    Logger.LogWarning("Ignoring the legacy weapon test-kit command; this update grants new elemental gems only.");
            }
            catch (Exception e)
            {
                Logger.LogError("ELEMENT_TEST_COMMAND " + body + ": " + e);
                deferCommand = parts.Length > 0 && parts[0] == "test-elemental-new-gems";
            }
            finally
            {
                if (deferCommand)
                {
                    nextElementalTestPoll = Time.unscaledTime + 5f;
                    continue;
                }
                var destination = Path.Combine(ProcessedDirectory, Path.GetFileName(file));
                Directory.CreateDirectory(ProcessedDirectory);
                if (File.Exists(file))
                {
                    if (File.Exists(destination)) destination = Path.Combine(ProcessedDirectory, System.Guid.NewGuid().ToString("N") + ".cmd");
                    File.Move(file, destination);
                }
            }
        }
    }

    private void SpawnElementalTrainingDummies()
    {
        if (SceneLoad.CurrentScene != Tableflip.Scene.Town_Grove) throw new InvalidOperationException("Load Grove village before spawning training dummies.");
        var player = Player.PlayerChar ?? throw new InvalidOperationException("Load a character first.");
        trainingDummies.RemoveWhere(dummy => dummy == null);
        foreach (var dead in trainingDummyPositions.Keys.Where(dummy => dummy == null).ToList()) trainingDummyPositions.Remove(dead);
        if (trainingDummies.Count > 0)
        {
            Logger.LogInfo("ELEMENT_TRAINING_DUMMIES already spawned for this town visit.");
            return;
        }
        var db = SingletonBehaviour<FateDB>.Instance;
        var elementalRecords = db.allMonsters
            .Where(monster => monster != null &&
                ((monster.refId ?? "").IndexOf("Elemental", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 (monster.displayName ?? "").IndexOf("Elemental", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 (monster.refId ?? "").IndexOf("Slime", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 (monster.displayName ?? "").IndexOf("Slime", StringComparison.OrdinalIgnoreCase) >= 0))
            .GroupBy(monster => Regex.Replace(monster.refId ?? "", "_\\d+$", ""))
            .Select(group => group.First())
            .ToList();
        var elementalTemplates = elementalRecords.Where(monster => monster.modelPrefab != null).ToList();
        Logger.LogInfo("ELEMENT_MONSTER_CATALOG " + string.Join(" | ", elementalRecords.Select(monster => $"{monster.refId}/{monster.displayName}/prefab={(monster.modelPrefab != null)}")));
        var plainTemplate = db.allMonsters
            .Where(monster => monster != null && monster.alignment == Alignment.Evil && monster.modelPrefab != null &&
                (monster.refId ?? "").IndexOf("Elemental", StringComparison.OrdinalIgnoreCase) < 0 &&
                (monster.displayName ?? "").IndexOf("Elemental", StringComparison.OrdinalIgnoreCase) < 0 &&
                (monster.refId ?? "").IndexOf("Slime", StringComparison.OrdinalIgnoreCase) < 0 &&
                (monster.displayName ?? "").IndexOf("Slime", StringComparison.OrdinalIgnoreCase) < 0 &&
                (monster.damageResists == null || monster.damageResists.All(resist => resist == null || resist.value <= 0)))
            .OrderBy(monster => monster.baseLevel)
            .FirstOrDefault();
        if (elementalTemplates.Count == 0) throw new InvalidOperationException("No elemental slime or elemental monster prefabs were found.");
        if (plainTemplate == null) throw new InvalidOperationException("No resistance-free monster prefab was found.");
        var templates = elementalTemplates.Concat(new[] { plainTemplate }).ToList();
        var portal = Object.FindObjectsOfType<EnterDungeon>()
            .Where(candidate => candidate != null && candidate.gameObject.activeInHierarchy)
            .OrderBy(candidate => (candidate.transform.position - player.transform.position).sqrMagnitude)
            .FirstOrDefault();
        if (portal == null) throw new InvalidOperationException("Could not find the active Grove dungeon entrance to place the targets in the field south of it.");
        var fieldCenter = portal.transform.position + Vector3.back * 10f;
        var count = 0;
        for (var index = 0; index < templates.Count; index++)
        {
            var template = templates[index];
            var sideOffset = (index - (templates.Count - 1) / 2f) * 2.25f;
            var position = fieldCenter + Vector3.right * sideOffset;
            if (!NavMesh.SamplePosition(position, out var hit, 6f, NavMesh.AllAreas))
            {
                Logger.LogWarning($"ELEMENT_DUMMY_POSITION no NavMesh near proposed south-field point {position}; skipping {template.displayName}.");
                continue;
            }
            position = hit.position;
            var gameObject = Object.Instantiate(template.modelPrefab, position, Quaternion.identity);
            if (!gameObject.TryGetComponent<Monster>(out var dummy)) { Object.Destroy(gameObject); continue; }
            var copy = Object.Instantiate(template);
            dummyIndex++;
            copy.alignment = Alignment.Evil;
            copy.minions = new List<MinionData>();
            dummy.monsterData = copy;
            dummy.Init();
            dummy.SetPositionProper(position);
            dummy.CharacterName = template.displayName + " Training Target";
            dummy.isUnique = true;
            dummy.allowDropEquipment = false;
            dummy.allowDropInventory = false;
            dummy.hp = 1000000f;
            dummy.maxHpBase = 1000000;
            dummy.SetTarget(null);
            dummy.SetAIState(AIState.Inactive);
            var agent = dummy.GetComponent<NavMeshAgent>();
            if (agent != null) { agent.ResetPath(); agent.isStopped = true; }
            var body = dummy.GetComponent<Rigidbody>();
            if (body != null) { body.velocity = Vector3.zero; body.angularVelocity = Vector3.zero; body.isKinematic = true; }
            trainingDummies.Add(dummy);
            trainingDummyPositions[dummy] = position;
            count++;
        }
        Logger.LogInfo($"ELEMENT_TRAINING_DUMMIES spawned {count} elemental and resistance-free village targets in the field south of the Grove dungeon portal at {fieldCenter}: {string.Join(", ", templates.Select(template => template.displayName))}.");
    }

    private void LockTrainingDummies()
    {
        foreach (var entry in trainingDummyPositions.ToList())
        {
            var dummy = entry.Key;
            if (dummy == null) { trainingDummyPositions.Remove(entry.Key); continue; }
            var transform = dummy.transform;
            if ((transform.position - entry.Value).sqrMagnitude > .0025f) transform.position = entry.Value;
            var agent = dummy.GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled) { agent.isStopped = true; agent.ResetPath(); }
            var body = dummy.GetComponent<Rigidbody>();
            if (body != null) { body.velocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
        }
    }
}
