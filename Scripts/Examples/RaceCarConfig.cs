using UnityEngine;

namespace SecretZauce.SecondBrain.Examples
{
    public class RaceCarConfig : ScriptableObject
    {
        [Tooltip("Top speed in units per second")]
        public float speed;

        [Tooltip("Hit points before the car is destroyed")]
        public float durability;

        [Tooltip("Cornering grip, 0 = no grip, 1 = perfect")]
        [Range(0f, 1f)]
        public float handling;

        [Tooltip("Normalised acceleration over time. X = elapsed time 0-1, Y = speed fraction 0-1")]
        public AnimationCurve accelerationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    }
}
