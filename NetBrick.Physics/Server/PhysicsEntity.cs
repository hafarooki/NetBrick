using System;
using BEPUphysics;
using BEPUphysics.Entities;
using BEPUphysics.CollisionShapes;

namespace NetBrick.Physics.Server
{
    public abstract class PhysicsEntity
    {
        public Entity Entity { get; }

        protected PhysicsEntity(EntityShape shape)
        {
            Entity = new Entity(shape);
        }
    }
}