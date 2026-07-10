namespace Enemy
{
    public abstract class EnemyConditionNode : Behavior
    {
        protected readonly EnemyBlackboard blackboard;

        protected EnemyConditionNode(EnemyBlackboard blackboard)
        {
            this.blackboard = blackboard;
        }

        protected override EStatus OnUpdate()
        {
            if (blackboard == null)
            {
                return EStatus.Failure;
            }

            return CheckCondition() ? EStatus.Success : EStatus.Failure;
        }

        protected abstract bool CheckCondition();
    }

    public abstract class EnemyActionNode : Behavior
    {
        protected readonly EnemyBlackboard blackboard;

        protected EnemyActionNode(EnemyBlackboard blackboard)
        {
            this.blackboard = blackboard;
        }
    }

    public class IsEnemyDeadNode : EnemyConditionNode
    {
        public IsEnemyDeadNode(EnemyBlackboard blackboard) : base(blackboard)
        {
        }

        protected override bool CheckCondition()
        {
            return blackboard.isDead;
        }
    }

    public class IsHitStunnedNode : EnemyConditionNode
    {
        public IsHitStunnedNode(EnemyBlackboard blackboard) : base(blackboard)
        {
        }

        protected override bool CheckCondition()
        {
            return blackboard.isHitStunned;
        }
    }

    public class IsTargetInAttackRangeNode : EnemyConditionNode
    {
        public IsTargetInAttackRangeNode(EnemyBlackboard blackboard) : base(blackboard)
        {
        }

        protected override bool CheckCondition()
        {
            return blackboard.isTargetInAttackRange;
        }
    }

    public class CanSeeTargetNode : EnemyConditionNode
    {
        public CanSeeTargetNode(EnemyBlackboard blackboard) : base(blackboard)
        {
        }

        protected override bool CheckCondition()
        {
            return blackboard.canSeeTarget;
        }
    }

    public class RequestEnemyStateNode : EnemyActionNode
    {
        private readonly EnemyStateType stateType;

        public RequestEnemyStateNode(EnemyBlackboard blackboard, EnemyStateType stateType) : base(blackboard)
        {
            this.stateType = stateType;
        }

        protected override EStatus OnUpdate()
        {
            if (blackboard == null)
            {
                return EStatus.Failure;
            }

            blackboard.RequestState(stateType);
            return EStatus.Success;
        }
    }
}
