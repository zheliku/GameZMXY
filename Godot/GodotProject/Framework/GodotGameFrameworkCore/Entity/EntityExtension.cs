using GameConfig.Entity;
using GameFramework.Entity;
using Godot;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GodotGameFramework.Entity
{
    /// <summary>
    /// 实体组件扩展方法。
    /// </summary>
    public static class EntityExtension
    {
        private static int s_NextEntityId;

        #region 显示实体
        public static int ShowEntity(this EntityComponent entityComponent, EEntityId entityId, object userData = null)
        {
            if (ConfigSystem.Instance.Tables.TbEntityConfig?.DataList == null)
            {
                Log.Error("EntityConfig data table is not available.");
                return -1;
            }
            EntityConfig cfg = ConfigSystem.Instance.Tables.TbEntityConfig.DataList.FirstOrDefault(x => x.EntityId == entityId);
            if (cfg == null)
            {
                Log.Error($"Entity {entityId} not found in EntityConfig");
                return -1;
            }

            int ser = Interlocked.Increment(ref s_NextEntityId);
            entityComponent.ShowEntity(ser, cfg.AssetPath, cfg.EntityGroupName, userData);
            return ser;
        }
        public static int ShowEntity(this EntityComponent entityComponent, string assetPath, string entityGroupName, object userData = null)
        {
            int ser = Interlocked.Increment(ref s_NextEntityId);
            entityComponent.ShowEntity(ser, assetPath, entityGroupName, userData);
            return ser;
        }
        /// <summary>
        /// 异步显示实体
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="entityComponent"></param>
        /// <param name="entityId"></param>
        /// <param name="userData"></param>
        /// <returns></returns>
        public static async Task<T> ShowEntityAsync<T>(this EntityComponent entityComponent, EEntityId entityId, object userData = null) where T : Node, IEntity
        {
            if (ConfigSystem.Instance.Tables.TbEntityConfig?.DataList == null)
            {
                Log.Error("EntityConfig data table is not available.");
                return null;
            }
            EntityConfig cfg = ConfigSystem.Instance.Tables.TbEntityConfig.DataList.FirstOrDefault(x => x.EntityId == entityId);
            if (cfg == null)
            {
                Log.Error($"Entity {entityId} not found in EntityConfig");
                return null;
            }

            int ser = Interlocked.Increment(ref s_NextEntityId);
            IEntity entity = await entityComponent.ShowEntityAsync(ser, cfg.AssetPath, cfg.EntityGroupName, userData);
            return entity as T;
        }
        public static async Task<IEntity> ShowEntityAsync(this EntityComponent entityComponent, EEntityId entityId, object userData = null)
        {
            if (ConfigSystem.Instance.Tables.TbEntityConfig?.DataList == null)
            {
                Log.Error("EntityConfig data table is not available.");
                return null;
            }
            EntityConfig cfg = ConfigSystem.Instance.Tables.TbEntityConfig.DataList.FirstOrDefault(x => x.EntityId == entityId);
            if (cfg == null)
            {
                Log.Error($"Entity {entityId} not found in EntityConfig");
                return null;
            }

            int ser = Interlocked.Increment(ref s_NextEntityId);
            IEntity entity = await entityComponent.ShowEntityAsync(ser, cfg.AssetPath, cfg.EntityGroupName, userData);
            return entity;
        }

        #endregion

        /// <summary>
        /// 安全隐藏实体。
        ///
        /// 在隐藏前检查实体是否存在和正在加载中，
        /// 避免因实体不存在而抛出异常。
        ///
        /// 适用于不确定实体是否还存活的场景（如实体可能已自行销毁）。
        /// </summary>
        /// <param name="entityComponent">实体组件。</param>
        /// <param name="entityId">实体编号。</param>
        /// <param name="userData">用户自定义数据。</param>
        public static void HideEntitySafe(this EntityComponent entityComponent, int entityId, object userData = null)
        {
            if (entityComponent.HasEntity(entityId) && !entityComponent.IsLoadingEntity(entityId))
            {
                entityComponent.HideEntity(entityId, userData);
            }
        }

        /// <summary>
        /// 通过实体编号安全隐藏实体。
        ///
        /// 在隐藏前检查实体引用是否有效。
        /// </summary>
        /// <param name="entityComponent">实体组件。</param>
        /// <param name="entity">要隐藏的实体。</param>
        /// <param name="userData">用户自定义数据。</param>
        public static void HideEntitySafe(this EntityComponent entityComponent, IEntity entity, object userData = null)
        {
            if (entity != null && entityComponent.IsValidEntity(entity))
            {
                entityComponent.HideEntity(entity, userData);
            }
        }
    }
}
