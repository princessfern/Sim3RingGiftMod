using System;
using System.Collections.Generic;
using System.Reflection;
using Sims3.Gameplay.Abstracts;
using Sims3.Gameplay.Actors;
using Sims3.Gameplay.Autonomy;
using Sims3.Gameplay.Interactions;
using Sims3.Gameplay.Objects;
using Sims3.Gameplay.Utilities;
using Sims3.SimIFace;
using Sims3.UI;

namespace Omnipotence.WeddingMod 
{
    [Persistable]
    public class WeddingRingBox : GameObject 
    {
        private Sim mOwner = null;

        public override void OnStartup()
        {
            base.OnStartup();
            
            // Safety gate: The Buy Mode 'ghost' doesn't have a Proxy or InWorld status.
            // This prevents the interaction from being added before the object is placed.
            if (this.InWorld && this.Proxy != null) 
            {
                this.AddInteraction(GiveRingInteraction.Singleton);
            }
        }

        private ulong GetGuidByPrice()
        {
            // Fallback to Tier 1 hash if catalog data is unavailable
            if (this.Product == null) return 0xCB3E96102AF4BB28; 

            int price = (int)this.Product.Price;
            if (price >= 30000) return ResourceUtils.HashString64("WeddingBand_Rock");
            if (price >= 10000) return ResourceUtils.HashString64("WeddingBand_showy");
            if (price >= 2500)  return ResourceUtils.HashString64("WeddingBand_nice");
            return ResourceUtils.HashString64("WeddingBand_Cheap");
        }

        public void OnHoppedInventory()
        {
            if (mOwner != null && mOwner.BuffManager != null)
            {
                mOwner.BuffManager.RemoveElement(GetGuidByPrice());
                mOwner = null;
            }
            CheckForOwnerAndApplyBuff();
        }

        private void CheckForOwnerAndApplyBuff()
        {
            // Basic Parent checking for Sim or Inventory ownership
            if (this.Parent is Sim)
            {
                mOwner = this.Parent as Sim;
            }
            else if (this.Parent != null)
            {
                GameObject parentObj = this.Parent as GameObject;
                if (parentObj != null && parentObj.Parent is Sim)
                {
                    mOwner = parentObj.Parent as Sim;
                }
            }

            if (mOwner != null && mOwner.BuffManager != null)
            {
                try 
                {
                    // Full Reflection approach to avoid 'Origin' enum compiler errors.
                    // We scan for AddElement(ulong, [any enum/int])
                    MethodInfo[] methods = mOwner.BuffManager.GetType().GetMethods();
                    foreach (MethodInfo method in methods)
                    {
                        if (method.Name == "AddElement")
                        {
                            ParameterInfo[] parameters = method.GetParameters();
                            if (parameters.Length == 2 && parameters[0].ParameterType == typeof(ulong))
                            {
                                // Invoke with our Guid and the integer 4 (Origin.FromGifting)
                                method.Invoke(mOwner.BuffManager, new object[] { GetGuidByPrice(), 4 });
                                break;
                            }
                        }
                    }
                }
                catch 
                {
                    // Silent fail ensures the game stays alive even if buff logic fails
                }
            }
        }
    }

    public class GiveRingInteraction : ImmediateInteraction<Sim, WeddingRingBox> 
    {
        public readonly static InteractionDefinition Singleton = new Definition();

        protected override bool Run() 
        {
            if (Actor != null && Target != null)
            {
                // Simple gifting animation
                Actor.PlaySoloAnimation("a2o_gift_give", true);
                
                // Move object from world/parent to inventory
                Target.UnParent();
                if (Actor.Inventory != null)
                {
                    Actor.Inventory.TryToAdd(Target);
                }
            }
            return true;
        }

        public class Definition : InteractionDefinition<Sim, WeddingRingBox, GiveRingInteraction>
        {
            protected override string GetInteractionName(Sim actor, WeddingRingBox target, InteractionObjectPair iop)
            {
                return "Give Wedding Ring";
            }

            protected override bool Test(Sim actor, WeddingRingBox target, bool isAutonomy, ref GreyedOutTooltipCallback greyedOutTooltipCallback)
            {
                // Always available as long as the Sim can reach the object
                return true; 
            }
        }
    }
}