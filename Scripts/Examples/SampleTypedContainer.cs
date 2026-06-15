using System.Collections.Generic;
using UnityEngine;

namespace SecretZauce.SecondBrain.Examples
{
    [CreateAssetMenu(menuName = "Create SampleTypedContainer", fileName = "SampleTypedContainer", order = 0)]
    [CreateChild("Examples")]
    public class SampleTypedContainer : TypedContainer<SampleConfig>, IHasCreateChildOption
    {
        // Provide two create-child options for SampleConfig: a default and a "magic" preset.
        // These demonstrate how multiple options for the same Type can offer different
        // initial values for the created child.
        public IReadOnlyList<CreateChildOption> GetCreateChildOptions()
        {
            return new List<CreateChildOption>
            {
                new CreateChildOption(
                    typeof(SampleConfig),
                    "Default Sample",
                    (obj, t) => {
                        if (obj is SampleConfig s)
                        {
                            s.atk = 1f;
                            s.def = 1f;
                            s.hp = 10f;
                        }
                    }),

                new CreateChildOption(
                    typeof(SampleConfig),
                    "Magic Sample",
                    (obj, t) => {
                        if (obj is SampleConfig s)
                        {
                            // Some "magic" preset values useful for demos / quick prototyping.
                            s.atk = 42f;
                            s.def = 13f;
                            s.hp = 999f;
                        }
                    })
            };
        }
    }
}