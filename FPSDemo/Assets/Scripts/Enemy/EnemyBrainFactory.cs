namespace Enemy
{
    /// <summary>
    /// 敌人行为树工厂
    /// 当前先用代码生成近战僵尸行为树，后续可替换为外部行为树资源
    /// </summary>
    public static class EnemyBrainFactory
    {
        public const string DefaultZombieMeleeTreeKey = "ZombieMelee";

        public static BehaviorTree Build(string behaviorTreeKey, EnemyBlackboard blackboard)
        {
            string safeKey = string.IsNullOrEmpty(behaviorTreeKey)
                ? DefaultZombieMeleeTreeKey
                : behaviorTreeKey;

            switch (safeKey)
            {
                case DefaultZombieMeleeTreeKey:
                default:
                    return BuildZombieMeleeTree(blackboard);
            }
        }

        private static BehaviorTree BuildZombieMeleeTree(EnemyBlackboard blackboard)
        {
            // 优先级从上到下，死亡和受击永远打断追击和攻击
            ActiveSelector root = new ActiveSelector();

            Sequence deadSequence = new Sequence();
            deadSequence.AddChild(new IsEnemyDeadNode(blackboard));
            deadSequence.AddChild(new RequestEnemyStateNode(blackboard, EnemyStateType.Dead));
            root.AddChild(deadSequence);

            Sequence hitSequence = new Sequence();
            hitSequence.AddChild(new IsHitStunnedNode(blackboard));
            hitSequence.AddChild(new RequestEnemyStateNode(blackboard, EnemyStateType.Hit));
            root.AddChild(hitSequence);

            Sequence attackSequence = new Sequence();
            attackSequence.AddChild(new HasAttackSlotNode(blackboard));
            attackSequence.AddChild(new IsTargetInAttackRangeNode(blackboard));
            attackSequence.AddChild(new RequestEnemyStateNode(blackboard, EnemyStateType.Attack));
            root.AddChild(attackSequence);

            Sequence chaseSequence = new Sequence();
            chaseSequence.AddChild(new CanSeeTargetNode(blackboard));
            chaseSequence.AddChild(new RequestEnemyStateNode(blackboard, EnemyStateType.Chase));
            root.AddChild(chaseSequence);

            root.AddChild(new RequestEnemyStateNode(blackboard, EnemyStateType.Idle));
            return new BehaviorTree(root);
        }
    }
}
