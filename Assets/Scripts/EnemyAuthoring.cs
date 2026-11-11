using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Burst;

public struct EnemyTag : IComponentData {}

[RequireComponent(typeof(CharacterAuthoring))]
public class EnemyAuthoring : MonoBehaviour
{
    private class Baker : Baker<EnemyAuthoring>
    {
        public override void Bake(EnemyAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<EnemyTag>(entity);
        }
    }
}

public partial struct EnemyMoveToPlayerSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerTag>();
    }
    public void OnUpdate(ref SystemState state)
    {
        var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();
        var playerPosition = SystemAPI.GetComponent<LocalTransform>(playerEntity).Position.xy;
        var moveToPlayerJob = new EnemyMoveToPlayerJob
        {
            PlayerPosition = playerPosition
        };
                                                                                                //这里的state.Dependency = ... 的含义是：把新Job的句柄赋值给系统状态的依赖句柄   
        state.Dependency = moveToPlayerJob.ScheduleParallel(state.Dependency);                  //ScheduleParallel(state.Dependency) 把旧的依赖作为新 Job 的前置依赖传进去                   
    }                                                                                           //state.Dependency是一个JobHandle类型的属性，用于管理作业之间的依赖关系。它确保在调度新的作业之前，所有依赖的作业都已经完成，从而避免数据竞争和潜在的错误
}
[BurstCompile]
[WithAll(typeof(EnemyTag))]
public partial struct EnemyMoveToPlayerJob : IJobEntity
{
    public float2 PlayerPosition;
    private void Execute(ref CharacterMoveDirection direction, in LocalTransform transform)
    {
        var vectorToPlayer  = PlayerPosition - transform.Position.xy;
        direction.Value = math.normalize(vectorToPlayer);
    }
}