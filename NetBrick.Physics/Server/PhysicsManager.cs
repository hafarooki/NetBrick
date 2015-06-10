using BEPUphysics;
using System.Threading;
using System;

namespace NetBrick.Physics.Server
{
    public class PhysicsManager
    {
        private readonly Space _space;

        public PhysicsManager()
        {
            _space = new Space();
            new Thread(() => Update()).Start();
        }

        private void Update()
        {
            while (!Environment.HasShutdownStarted)
            {
                _space.Update();
                Thread.Sleep(10);
            }
        }

        public void Add(PhysicsEntity entity)
        {
            _space.Add(entity.Entity);
        }

        public void Remove(PhysicsEntity entity)
        {
            _space.Add(entity.Entity);
        }
    }
}
