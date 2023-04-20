﻿using System;

namespace RainMeadow
{
    public class PhysicalObjectEntityState : EntityState
    {
        public WorldCoordinate pos;
        public bool realized;
        public OnlineState realizedObjectState;

        public PhysicalObjectEntityState() : base() { }
        public PhysicalObjectEntityState(OnlineEntity onlineEntity, ulong ts, bool realizedState) : base(onlineEntity, ts, realizedState)
        {
            this.pos = onlineEntity.entity.pos;
            this.realized = onlineEntity.realized; // now now, oe.realized means its realized in the owners world
                                                   // not necessarily whether we're getting a real state or not
            if (realizedState) this.realizedObjectState = GetRealizedState();
        }

        protected virtual RealizedObjectState GetRealizedState()
        {
            if (onlineEntity.entity.realizedObject == null) throw new InvalidOperationException("not realized");
            return new RealizedObjectState(onlineEntity);
        }

        public override StateType stateType => StateType.PhysicalObjectEntityState;

        public override void ReadTo(OnlineEntity onlineEntity) // idk why this has a param if it also stores a ref to it
        {
            //onlineEntity.entity.pos = pos;
            onlineEntity.entity.Move(pos);
            onlineEntity.realized = this.realized;
            (realizedObjectState as RealizedObjectState)?.ReadTo(onlineEntity);
        }

        public override void CustomSerialize(Serializer serializer)
        {
            base.CustomSerialize(serializer);
            serializer.SerializeNoStrings(ref pos);
            serializer.Serialize(ref realized);
            serializer.SerializeNullable(ref realizedObjectState);
        }
    }

    public class CreatureEntityState : PhysicalObjectEntityState
    {
        // what do I even put here for AbstractCreature? inDen?
        public CreatureEntityState() : base() { }
        public CreatureEntityState(OnlineEntity onlineEntity, ulong ts, bool realizedState) : base(onlineEntity, ts, realizedState)
        {

        }

        protected override RealizedObjectState GetRealizedState()
        {
            if (onlineEntity.entity.realizedObject is Player) return new RealizedPlayerState(onlineEntity);
            if (onlineEntity.entity.realizedObject is Creature) return new RealizedCreatureState(onlineEntity);
            return base.GetRealizedState();
        }

        public override StateType stateType => StateType.CreatureEntityState;
    }

}