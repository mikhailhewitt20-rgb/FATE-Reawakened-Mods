using Cleverous.VaultInventory;
using Tableflip;
using UnityEngine;

namespace MikhailKillsZombies.FatePermanentStorage
{
 public sealed class StorageRoomDoor : InteractableSceneObject
 {
  StorageRoomManager manager;

  public void Init(StorageRoomManager owner)
  {
   manager = owner;
   InteractableName = "Permanent Storage Room";
   InteractRange = 3.5f;
   InteractionPriority = 20;
  }

  public override void Interact(Character interactor = null)
  {
   StorageDoorAudio.Play(transform.position);
   manager?.EnterRoom(manager.EntranceReturnPosition, true);
  }
 }

 public sealed class StorageExitDoor : InteractableSceneObject
 {
  StorageRoomManager manager;

  public void Init(StorageRoomManager owner)
  {
   manager = owner;
   InteractableName = "Return to the Village";
   InteractRange = 2.4f;
   InteractionPriority = 5;
  }

  public override void Interact(Character interactor = null)
  {
   StorageDoorAudio.Play(transform.position);
   manager?.ExitRoom();
  }
 }

 // Marker used by Harmony patches to keep the invisible storage proxy out of the normal stash
 // save/load path. It shares the Stash implementation but never owns the player's town stash.
 public sealed class StorageStashProxy : MonoBehaviour
 {
 }
}
