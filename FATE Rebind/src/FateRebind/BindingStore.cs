using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Xml;
using System.Security.Cryptography;
using System.Text;
namespace ZombieGhost.Rebind
{
 public sealed class BindingStore
 {
  private readonly Dictionary<string,string> values=new Dictionary<string,string>(StringComparer.Ordinal);
  private readonly string file;
  public BindingStore(string file){this.file=file;}
  public int Count=>values.Count;
  public bool TryGet(string key,out string path)=>values.TryGetValue(key,out path);
  public void Set(string key,string path)
  {if(string.IsNullOrEmpty(key)||key.Length>600||path==null||path.Length>512||!values.ContainsKey(key)&&values.Count>=4096)throw new ArgumentException("Invalid binding entry");values[key]=path;}
  public void Remove(string key)=>values.Remove(key);
  public void Load()
  {
   if(!File.Exists(file))return;
   using var reader=XmlReader.Create(file,new XmlReaderSettings {DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=2000000});
   var document=XDocument.Load(reader);
   if(document.Root?.Name!="FateRebind" || (string)document.Root.Attribute("version")!="1")throw new InvalidDataException("Unsupported rebind settings format");
   var loaded=new Dictionary<string,string>(StringComparer.Ordinal);
   foreach(var entry in document.Root.Elements("Binding"))
   {
    string key=(string)entry.Attribute("key"),path=(string)entry.Attribute("path");
    if(string.IsNullOrEmpty(key)||key.Length>600||path==null||path.Length>512||loaded.Count>=4096||loaded.ContainsKey(key))throw new InvalidDataException("Invalid rebind settings entry");
    loaded[key]=path;
   }
   values.Clear();foreach(var pair in loaded)values[pair.Key]=pair.Value;
  }
  public void Save()
  {
   var directory=Path.GetDirectoryName(Path.GetFullPath(file));Directory.CreateDirectory(directory);
   var root=new XElement("FateRebind",new XAttribute("version","1"),values.OrderBy(p=>p.Key,StringComparer.Ordinal).Select(p=>new XElement("Binding",new XAttribute("key",p.Key),new XAttribute("path",p.Value))));
   string temporary=file+".tmp";new XDocument(root).Save(temporary);
   if(File.Exists(file))File.Replace(temporary,file,file+".bak");else File.Move(temporary,file);
  }
 }
 public static class BindingIdentity
 {
  public static string Key(string provider,string action,string binding)=>provider+"|"+action+"|"+binding;
  public static bool Overlaps(string ownerA,string mapA,string ownerB,string mapB)=>ownerA!="game" || ownerB!="game" || mapA==mapB || mapA=="Persistent" || mapB=="Persistent" || string.IsNullOrEmpty(mapA) || string.IsNullOrEmpty(mapB);
  public static bool SameControl(string a,string b)=>!string.IsNullOrEmpty(a)&&!string.IsNullOrEmpty(b)&&string.Equals(a,b,StringComparison.OrdinalIgnoreCase);
  public static string StableToken(string text){using var hash=SHA256.Create();return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-","").ToLowerInvariant();}
 }
}
