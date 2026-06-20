using System.Collections.Generic;
using UnityEngine;

namespace SecretZauce.SecondBrain.Examples
{
    [CreateAssetMenu(menuName = "Racing/Race Car Container", fileName = "RaceCarContainer", order = 0)]
    [CreateChild("Examples")]
    public class RaceCarContainer : TypedContainer<RaceCarConfig>, IHasCreateChildOption
    {
        // Each option corresponds to a car archetype with a distinct stat profile and
        // a hand-authored acceleration curve that reflects how that archetype feels to drive.
        public IReadOnlyList<CreateChildOption> GetCreateChildOptions()
        {
            return new List<CreateChildOption>
            {
                new CreateChildOption(
                    typeof(RaceCarConfig),
                    "Sports Car",
                    (obj, t) =>
                    {
                        if (obj is not RaceCarConfig car) return;
                        car.speed      = 220f;
                        car.durability = 60f;
                        car.handling   = 0.85f;
                        // Punchy launch: hits 80 % speed in the first 30 % of the rev range,
                        // then tapers to top speed.
                        car.accelerationCurve = new AnimationCurve(
                            new Keyframe(0f,    0f,   0f, 4f),
                            new Keyframe(0.3f,  0.8f, 2f, 0.5f),
                            new Keyframe(1f,    1f,   0f, 0f)
                        );
                    }),

                new CreateChildOption(
                    typeof(RaceCarConfig),
                    "Heavy Tank",
                    (obj, t) =>
                    {
                        if (obj is not RaceCarConfig car) return;
                        car.speed      = 140f;
                        car.durability = 200f;
                        car.handling   = 0.35f;
                        // Slow, linear grind — speed builds almost uniformly over the full range.
                        car.accelerationCurve = new AnimationCurve(
                            new Keyframe(0f,   0f,   0f, 0.8f),
                            new Keyframe(0.5f, 0.4f, 0.8f, 0.8f),
                            new Keyframe(1f,   1f,   1.2f, 0f)
                        );
                    }),

                new CreateChildOption(
                    typeof(RaceCarConfig),
                    "Balanced Racer",
                    (obj, t) =>
                    {
                        if (obj is not RaceCarConfig car) return;
                        car.speed      = 180f;
                        car.durability = 120f;
                        car.handling   = 0.65f;
                        // Classic S-curve: smooth build, peak thrust in the mid-range, gentle taper.
                        car.accelerationCurve = new AnimationCurve(
                            new Keyframe(0f,    0f,   0f,   1.5f),
                            new Keyframe(0.25f, 0.25f, 1.5f, 2.5f),
                            new Keyframe(0.6f,  0.75f, 2f,   0.8f),
                            new Keyframe(1f,    1f,   0.3f, 0f)
                        );
                    })
            };
        }
    }
}
