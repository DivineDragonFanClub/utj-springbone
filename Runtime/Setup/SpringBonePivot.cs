using UnityEngine;
using UTJ.Jobs;

namespace UTJ
{
    [ExecuteInEditMode]
    public class SpringBonePivot : MonoBehaviour
    {
#if UNITY_EDITOR
        void Update()
        {
            if (transform.hasChanged)
            {
                transform.hasChanged = false;
                var myManager = GetComponentInParent<SpringJobManager>();
                if (myManager != null)
                {
                    SpringJobManager.UpdateBoneList(myManager);
                }
            }
        }

        public void OnValidate()
        {
            var myManager = GetComponentInParent<SpringJobManager>();
            if (myManager != null)
            {
                SpringJobManager.UpdateBoneList(myManager);
            }
        }
    }
#endif
}