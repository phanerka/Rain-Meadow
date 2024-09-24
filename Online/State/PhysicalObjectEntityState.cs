﻿using System;
using System.Linq;

namespace RainMeadow
{
    public class PhysicalObjectEntityState : OnlineEntity.EntityState
    {
        [OnlineField(group = "realized")]
        public WorldCoordinate pos;
        [OnlineField]
        public bool inDen;
        [OnlineField(nullable=true)]
        public Generics.DynamicOrderedStates<AbstractObjStickRepr> sticks;
        [OnlineField]
        public bool realized;
        [OnlineField(group = "realized", nullable = true, polymorphic = true)]
        public RealizedPhysicalObjectState realizedObjectState;

        public PhysicalObjectEntityState() : base() { }
        public PhysicalObjectEntityState(OnlinePhysicalObject onlineEntity, OnlineResource inResource, uint ts) : base(onlineEntity, inResource, ts)
        {
            var realizedState = inResource is RoomSession;
            if (realizedState && onlineEntity.isMine && onlineEntity.apo.realizedObject != null && !onlineEntity.realized) { RainMeadow.Error($"have realized object, but entity not marked as realized??: {onlineEntity} in resource {inResource}"); }
            if (realizedState && onlineEntity.isMine && !onlineEntity.realized)
            {
                RainMeadow.Trace($"asked for realized state, not realized: {onlineEntity} in resource {inResource}");
                realizedState = false;
            }
            if(realizedState && onlineEntity.apo.realizedObject == null)
            {
                RainMeadow.Error($"asked for realized state, not realized: {onlineEntity} in resource {inResource}");
                realizedState = false;
            }
            RainMeadow.Trace($"{onlineEntity} sending realized state? {realizedState} entity realized ? {onlineEntity.realized}");

            this.pos = onlineEntity.apo.pos;
            this.inDen = onlineEntity.apo.InDen;
            this.sticks = new(onlineEntity.apo.stuckObjects.Where(s => s.A == onlineEntity.apo).Select(s => AbstractObjStickRepr.map.GetValue(s, AbstractObjStickRepr.FromStick)).Where(s => s != null).ToList());
            this.realized = onlineEntity.realized; // now now, oe.realized means its realized in the owners world
                                                   // not necessarily whether we're getting a real state or not
            if (realizedState) this.realizedObjectState = GetRealizedState(onlineEntity);
        }

        protected virtual RealizedPhysicalObjectState GetRealizedState(OnlinePhysicalObject onlineObject)
        {

            if (onlineObject.apo.realizedObject == null) throw new InvalidOperationException("not realized");
            if (onlineObject.apo.realizedObject is Spear) return new RealizedSpearState(onlineObject);
            if (onlineObject.apo.realizedObject is ScavengerBomb) return new RealizedScavengerBombState(onlineObject);
            if (onlineObject.apo.realizedObject is SporePlant) return new RealizedSporePlantState(onlineObject);
            if (onlineObject.apo.realizedObject is SlimeMold) return new RealizedSlimeMoldState(onlineObject);
            if (onlineObject.apo.realizedObject is VultureGrub) return new RealizedVultureGrubState(onlineObject);
            if (onlineObject.apo.realizedObject is DangleFruit) return new RealizedDangleFruitState(onlineObject);
            if (onlineObject.apo.realizedObject is Weapon) return new RealizedWeaponState(onlineObject); // Order matters here. If your item inherits from another class, that parent class should be lower

            return new RealizedPhysicalObjectState(onlineObject);
        }

        public override void ReadTo(OnlineEntity onlineEntity)
        {
            base.ReadTo(onlineEntity);
            if (onlineEntity.isPending) { RainMeadow.Debug($"not syncing {onlineEntity} because pending"); return; }; // Don't sync if pending, reduces visibility and effect of lag

            var onlineObject = onlineEntity as OnlinePhysicalObject;
            var apo = onlineObject.apo;
            RainMeadow.Trace($"{onlineEntity} received realized state? {realizedObjectState != null} entity realized?{onlineObject.realized}");

            var wasPos = apo.pos;
            try
            {
                if (pos.room == -1)
                {
                    if (wasPos.room != -1)
                    {
                        if(apo.realizedObject is PhysicalObject po)
                        {
                            po.RemoveFromRoom();
                            apo.Abstractize(wasPos);
                        }
                        apo.Room.RemoveEntity(apo);
                    }
                }
                else
                {
                    if (inDen != apo.InDen)
                    {
                        if (inDen)
                        {
                            RainMeadow.Debug("moving to den: " + onlineObject);
                            apo.IsEnteringDen(pos);
                        }
                        else
                        {
                            RainMeadow.Debug("moving out of den: " + onlineObject);
                            apo.IsExitingDen();
                        }
                    }
                    if (wasPos.room != -1)
                    {
                        apo.Move(pos);
                    }
                    else
                    {
                        if (apo.world.IsRoomInRegion(pos.room)) apo.world.GetAbstractRoom(pos).AddEntity(apo);
                    }
                }
            }
            catch (Exception e)
            {
                RainMeadow.Error($"{onlineEntity} failed to move from {wasPos} to {pos}, hard-setting position: " + e);
                if (apo.world.IsRoomInRegion(apo.pos.room)) apo.world.GetAbstractRoom(apo.pos).RemoveEntity(apo);
                apo.pos = pos;
                if (apo.world.IsRoomInRegion(pos.room)) apo.world.GetAbstractRoom(pos).AddEntity(apo);
                //throw;
            }
            
            onlineObject.apo.pos = pos; // pos isn't updated if compareDisregardingTile, but please, do

            // sticks
            bool[] found = new bool[apo.stuckObjects.Count];
            RainMeadow.Trace($"incoming sticks for {onlineEntity}: " + sticks.list.Count);
            for (int i = 0; i < sticks.list.Count; i++)
            {
                var stick = sticks.list[i];
                var a = onlineObject;
                var b = stick.b;
                if (a == null || b == null) continue;
                var foundat = apo.stuckObjects.FindIndex(s => stick.StickEquals(s, a, b));
                if (foundat == -1)
                {
                    RainMeadow.Trace("incoming stick not found: " + stick);
                    stick.MakeStick(onlineObject.apo);
                }
                else
                {
                    RainMeadow.Trace("incoming stick found: " + stick + " at index " + foundat);
                    found[foundat] = true;
                }
            }
            for (int i = found.Length - 1; i >= 0; i--)
            {
                if (!found[i] && apo.stuckObjects[i].A == apo)
                {
                    RainMeadow.Trace("releasing stick because not found at index " + i);
                    AbstractObjStickRepr.map.GetValue(apo.stuckObjects[i], AbstractObjStickRepr.FromStick)?.Release(apo.stuckObjects[i]);
                }
            }

            onlineObject.realized = this.realized;
            if (onlineObject.apo.realizedObject != null)
            {
                RainMeadow.Trace($"{onlineEntity} realized target exists");
                realizedObjectState?.ReadTo(onlineEntity);
            }
        }
    }
}
