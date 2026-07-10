using System.Collections.Generic;
using UnityEngine;

namespace Enemy
{
    public class EnemyPool : MonoBehaviour
    {
        private readonly Dictionary<GameObject, Queue<EnemyController>> _pool = new Dictionary<GameObject, Queue<EnemyController>>();

        public EnemyController Get(GameObject prefab)
        {
            if (prefab == null)
            {
                return null;
            }

            Queue<EnemyController> queue = GetQueue(prefab);
            while (queue.Count > 0)
            {
                EnemyController pooledEnemy = queue.Dequeue();
                if (pooledEnemy == null)
                {
                    continue;
                }

                pooledEnemy.transform.SetParent(null);
                return pooledEnemy;
            }

            GameObject instance = Instantiate(prefab);
            instance.SetActive(false);
            EnemyController enemy = instance.GetComponent<EnemyController>();
            if (enemy == null)
            {
                enemy = instance.AddComponent<EnemyController>();
            }

            return enemy;
        }

        public void Return(GameObject prefab, EnemyController enemy)
        {
            if (enemy == null)
            {
                return;
            }

            enemy.gameObject.SetActive(false);
            enemy.transform.SetParent(transform);

            if (prefab == null)
            {
                return;
            }

            GetQueue(prefab).Enqueue(enemy);
        }

        private Queue<EnemyController> GetQueue(GameObject prefab)
        {
            if (!_pool.TryGetValue(prefab, out Queue<EnemyController> queue))
            {
                queue = new Queue<EnemyController>();
                _pool.Add(prefab, queue);
            }

            return queue;
        }
    }
}
