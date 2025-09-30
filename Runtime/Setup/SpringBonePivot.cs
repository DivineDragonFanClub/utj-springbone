using UnityEngine;

namespace UTJ
{
    public class SpringBonePivot : MonoBehaviour
    {
#if UNITY_EDITOR
        private void OnValidate() {
            var myManager = this.GetComponentInParent<Jobs.SpringJobManager>();
            if (myManager == null) {
                return;
            }
            Jobs.SpringJobManager.UpdateBoneList(myManager);
        }
#endif
    }
}