using UnityEngine;

namespace UTJ
{
    public abstract class ISpringManager : MonoBehaviour
    {
        public abstract void Stabilize();

        // RVA: -1 Offset: -1 Slot: 5
        public abstract void UpdateSimulation();

        // RVA: -1 Offset: -1 Slot: 6
        public abstract void ResetSimulation();

        // RVA: -1 Offset: -1 Slot: 7
        public abstract void SetGravity(Vector3 gravity);

        // RVA: -1 Offset: -1 Slot: 8
        public abstract void SetGroundHeight(float groundHeight);

        // RVA: -1 Offset: -1 Slot: 9
        public abstract void SetDefaultWindParameter();

        // RVA: -1 Offset: -1 Slot: 10
        public abstract void SetWindPower(Vector3 windPower);

        // RVA: -1 Offset: -1 Slot: 11
        public abstract void SetWindSpeed(float windSpeed);

        // RVA: -1 Offset: -1 Slot: 12
        public abstract void SetWindDir(Vector3 windDir);

        // RVA: -1 Offset: -1 Slot: 13
        public abstract void ApplyLocalWind(Vector3 windPower, float windSpeed, Vector3 windDir, float startTime = 0.2f, float durationTime = 1, float decayTime = 0.2f);

        // RVA: -1 Offset: -1 Slot: 14
        public abstract void ReplaceJobManager();
    }
}