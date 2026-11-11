using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Burst;
using Unity.Rendering;

public struct InitializeCharacterFlag : IComponentData, IEnableableComponent
{
    // This struct is used as a flag component to indicate that the character entity has been initialized.
}
public struct CharacterMoveDirection : IComponentData
{
    public float2 Value;
}
public struct CharacterMoveSpeed : IComponentData
{
    public float Value;
}

[MaterialProperty("_FacingDirection")]
public struct FacingDirectionOverride : IComponentData
{
    public float Value;
}



public class CharacterAuthoring : MonoBehaviour
{
    public float MoveSpeed;
    //Baker将定义的组件数据转换为ECS组件数据
    private class Baker : Baker<CharacterAuthoring>
    {
        public override void Bake(CharacterAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);            //获取实体并指定其Transform使用标志为动态
            AddComponent<InitializeCharacterFlag>(entity);                 //添加InitializeCharacterFlag标记组件到实体
            AddComponent<CharacterMoveDirection>(entity);                   //添加CharacterMoveDirection等数据组件到实体
            AddComponent(entity, new CharacterMoveSpeed                     //这些组件将在运行时用于控制角色的移动方向和速度，
            {                                                               //并在Runtime的Inspector中可视化和调整这些参数
                Value = authoring.MoveSpeed
            });
            AddComponent(entity, new FacingDirectionOverride
            {
                Value = 1
            });
        }
    }
} 

/*
partial 关键字的作用：允许将一个类、结构体、接口或方法的定义拆分到多个文件（或多个位置）中。在编译时，C# 编译器会自动将这些部分拼接成一个完整的类型。
它最强大的地方在于“组合大于拆分”，特别是在配合代码生成工具或多人协作时非常有用。
这里的作用主要是：允许ECS源生成器为每一个类型分别追加代码片段，从而可以使用SystemAPI等新特性，而不需要手动编写大量样板代码。
*/
[UpdateInGroup(typeof(InitializationSystemGroup))]          //指定系统的更新顺序，确保在初始化阶段执行
public partial struct CharacterInitializeSystem : ISystem
{
    [BurstCompile]  
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (mass, shouldInitialize) in SystemAPI.Query<RefRW<PhysicsMass>, EnabledRefRW<InitializeCharacterFlag>>())
        {
            mass.ValueRW.InverseInertia = float3.zero;
            shouldInitialize.ValueRW = false;  //禁用InitializeCharacterFlag组件，表示初始化完成
        }
    }
}

public partial struct CharacterMoveSystem : ISystem
{
    [BurstCompile]  //启用Burst编译器优化，提高性能
    public void OnUpdate(ref SystemState state)
    {
        //Query方法用于查询ECS世界中所有具有指定组件的实体，并返回一个可枚举的集合
        foreach (var (velocity,facingDirection,direction,speed,entity) in SystemAPI.Query<RefRW<PhysicsVelocity>, RefRW<FacingDirectionOverride>, 
        CharacterMoveDirection, CharacterMoveSpeed>().WithEntityAccess())
        {   
            //遍历查询到的所有实体，并获取它们的LocalTransform、CharacterMoveDirection和CharacterMoveSpeed组件数据 
            var moveStep2D = direction.Value * speed.Value ; 
            velocity.ValueRW.Linear = new float3(moveStep2D, 0f); 
            
            if(math.abs(moveStep2D.x) > 0.15f)
            {
                facingDirection.ValueRW.Value = math.sign(moveStep2D.x);
            }

            if (SystemAPI.HasComponent<PlayerTag>(entity))
            {
                var animationOverride = SystemAPI.GetComponentRW<AnimationIndexOverride>(entity);
                var animationType = math.lengthsq(moveStep2D) > float.Epsilon ? PlayerAnimationIndex.Movement : PlayerAnimationIndex.Idle; 
                animationOverride.ValueRW.Value = (float)animationType;
            }
        }
    }
}

public partial struct GlobalTimeUpdateSystem : ISystem
{
    private static int _globalTimeShaderPropertyID;

    public void OnCreate(ref SystemState state)
    {
        _globalTimeShaderPropertyID =  Shader.PropertyToID("_GlobalTime");
    }
    public void OnUpdate(ref SystemState state)
    {
        Shader.SetGlobalFloat(_globalTimeShaderPropertyID, (float)SystemAPI.Time.ElapsedTime);
    }
}