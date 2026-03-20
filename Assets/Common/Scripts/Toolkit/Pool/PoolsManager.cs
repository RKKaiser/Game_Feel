using System.Collections.Generic;
using UnityEngine;

namespace OctoberStudio.Pool
{
    /// <summary>
    /// 对象池管理器
    /// 负责管理多个预加载的对象池，提供获取对象池和从池中获取实体的方法
    /// </summary>
    public class PoolsManager : MonoBehaviour
    {
        // 在Inspector中可配置的预加载池数据列表
        [SerializeField] List<PoolData> preloadedPools;

        // 存储所有对象池的字典，键为名称的哈希值，值为对应的池对象
        private Dictionary<int, PoolObject> pools;

        /// <summary>
        /// Unity Awake 方法，在游戏对象初始化时调用
        /// 初始化字典并根据预配置数据创建所有对象池
        /// </summary>
        private void Awake()
        {
            // 初始化字典
            pools = new Dictionary<int, PoolObject>();

            // 遍历所有预配置的池数据
            for (int i = 0; i < preloadedPools.Count; i++)
            {
                var data = preloadedPools[i];
                // 使用名称的哈希值作为字典的键，提高查找效率
                int hash = data.name.GetHashCode();
                // 创建新的对象池实例
                var pool = new PoolObject(data.name, data.prefab, data.size);
                // 将池添加到字典中
                pools[hash] = pool;
            }
        }

        /// <summary>
        /// 根据名称获取对象池
        /// </summary>
        /// <param name="name">池的名称</param>
        /// <returns>对应的 PoolObject 对象，如果不存在则返回 null</returns>
        public PoolObject GetPool(string name)
        {
            return GetPool(name.GetHashCode());
        }

        /// <summary>
        /// 根据哈希值获取对象池
        /// </summary>
        /// <param name="hash">池名称的哈希值</param>
        /// <returns>对应的 PoolObject 对象，如果不存在则返回 null</returns>
        public PoolObject GetPool(int hash)
        {
            if (pools.ContainsKey(hash))
            {
                return pools[hash];
            }
            return null;
        }

        /// <summary>
        /// 根据名称从对象池中获取一个游戏对象实体
        /// </summary>
        /// <param name="name">池的名称</param>
        /// <returns>获取到的 GameObject，如果池不存在或无法获取则返回 null</returns>
        public GameObject GetEntity(string name)
        {
            return GetEntity(name.GetHashCode());
        }

        /// <summary>
        /// 根据哈希值从对象池中获取一个游戏对象实体
        /// </summary>
        /// <param name="hash">池名称的哈希值</param>
        /// <returns>获取到的 GameObject，如果池不存在或无法获取则返回 null</returns>
        public GameObject GetEntity(int hash)
        {
            var pool = GetPool(hash);
            if (pool != null) return pool.GetEntity();
            return null;
        }

        /// <summary>
        /// 根据名称从对象池中获取一个特定类型的组件
        /// </summary>
        /// <typeparam name="T">需要获取的组件类型</typeparam>
        /// <param name="name">池的名称</param>
        /// <returns>获取到的组件，如果池不存在或无法获取则返回 null</returns>
        public T GetEntity<T>(string name) where T : Component
        {
            return GetEntity<T>(name.GetHashCode());
        }

        /// <summary>
        /// 根据哈希值从对象池中获取一个特定类型的组件
        /// </summary>
        /// <typeparam name="T">需要获取的组件类型</typeparam>
        /// <param name="hash">池名称的哈希值</param>
        /// <returns>获取到的组件，如果池不存在或无法获取则返回 null</returns>
        public T GetEntity<T>(int hash) where T : Component
        {
            var pool = GetPool(hash);
            if (pool != null) return pool.GetEntity<T>();
            return null;
        }

        /// <summary>
        /// 池数据配置类，用于在Inspector中配置每个对象池的信息
        /// </summary>
        [System.Serializable]
        private class PoolData
        {
            public string name;      // 池的名称
            public GameObject prefab; // 用于创建对象的预制体
            public int size;         // 池的初始大小（预创建的对象数量）
        }
    }
}