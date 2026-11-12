using UnityEngine;

namespace MikhailKillsZombies.FatePermanentStorage
{
 // Runtime identity and authored transform for one ceiling surround tile. These markers are
 // still used by the baked ceiling layout; the interactive editor that used to consume them
 // has been removed.
 public sealed class CeilingTileMarker : MonoBehaviour
 {
  public int GridX;
  public int GridZ;
  public Vector3 BaseWorldPosition;
  public Vector3 BaseLocalScale;
 }

 // Runtime identity and authored transform for one visual wall piece. Baked wall layouts still
 // use these markers when the room is rebuilt.
 public sealed class WallTileMarker : MonoBehaviour
 {
  public string Id;
  public string StyleKey;
  public int Type;
  public bool Added;
  public GameObject SourcePrefab;
  public Vector3 BaseWorldPosition;
  public Quaternion BaseWorldRotation;
 }
}
