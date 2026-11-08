using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;


public struct PlayerTag : IComponentData
{
    // This struct is used as a tag component to identify the player entity in the ECS world.
} 

public struct CameraTarget : IComponentData
{   //此处不用public Transform CameraTransform;
    //Transform 是传统的托管类（引用类型）。标准的非托管 struct 组件是不允许直接包含托管引用的，否则会破坏内存连续性，导致无法使用 Burst 编译器和 Job System
    public UnityObjectRef<Transform> CameraTransform;                       
    //UnityObjectRef在不破坏 ECS 非托管内存和性能（支持 Burst 与 Job）的前提下，让实体能安全地引用传统的 Transform 对象。内部只存储目标对象的 Instance ID（一个整数），而不是直接存储托管引用
}
public struct InitializeCameraTargetTag : IComponentData    {}


public class PlayerAuthoring : MonoBehaviour
{
    private class Baker : Baker<PlayerAuthoring>
    {
        public override void Bake(PlayerAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<PlayerTag>(entity);
            AddComponent<InitializeCameraTargetTag>(entity);
            AddComponent<CameraTarget>(entity);
        }
    }
}

/*
ISystem 配合 struct 能用 Burst 编译、性能更好，但不能直接存托管对象、不支持继承。                  --性能
SystemBase 虽然会分配 GC、不能 Burst 编译，但它支持继承，也允许你在系统里直接持有托管类型字段。     --便利
*/
[UpdateInGroup(typeof(InitializationSystemGroup))]                                                                  //指定系统的更新顺序，确保在初始化阶段执行
public partial struct CameraInitializeSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<InitializeCameraTargetTag>();                                                        //要求在系统更新前必须存在 InitializeCameraTargetTag 组件，否则系统不会执行
    }
    public void OnUpdate(ref SystemState state)
    {
        if(CameraTargetSingleton.Instance == null) return;                                                          //如果单例实例不存在，直接返回，避免空引用异常
        var cameraTransform = CameraTargetSingleton.Instance.transform;

        var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);                                             //创建一个实体命令缓冲区，用于在系统更新后执行实体操作，避免在迭代中直接修改实体集合 
        foreach(var (cameraTarget,entity) in SystemAPI.Query<RefRW<CameraTarget>>().WithAll<InitializeCameraTargetTag, PlayerTag>() 
        .WithEntityAccess() )                                                                                       //使用 WithEntityAccess() 允许在查询中获取实体的引用，以便在迭代中对实体进行操作
        {
            cameraTarget.ValueRW.CameraTransform = cameraTransform;                                                 //将单例的 Transform 引用赋值给 CameraTarget 组件
            ecb.RemoveComponent<InitializeCameraTargetTag>(entity);                                                 //在命令缓冲区中记录移除 InitializeCameraTargetTag 组件的操作，表示初始化完成
        } 
        ecb.Playback(state.EntityManager);                                                                          //执行命令缓冲区中的所有操作，确保实体状态更新
    }
}
public partial struct MoveCameraSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        foreach(var (transform, cameraTarget)  in SystemAPI.Query<LocalToWorld, CameraTarget>().WithAll<PlayerTag>()
        .WithNone<InitializeCameraTargetTag>())
        {
            cameraTarget.CameraTransform.Value.position = transform.Position;                   //将相机的 Transform 位置设置为玩家实体的 LocalToWorld 位置，实现相机跟随玩家移动
        }
    }    
}


public partial class PlayerInputSystem : SystemBase
{
    private SurvivorsInput _input;
    //protected 的含义是：只有当前类及其派生类可以访问，外部类不能直接调用
    //protected override 是 Unity ECS 生命周期方法的标准写法——框架能调，你不该手动调
    protected override void OnCreate()
    {
        _input = new SurvivorsInput();
    }

    protected override void OnStartRunning()
    {
        _input.asset.Enable();
    }

    protected override void OnStopRunning()
    {
        _input.asset.Disable();
    }

    protected override void OnDestroy()
    {
        _input.asset.Disable();
        _input.Dispose();
    }

    protected override void OnUpdate()
    {
        var currentInput = (float2)_input.Player.Move.ReadValue<Vector2>();
        foreach( var direction in SystemAPI.Query<RefRW<CharacterMoveDirection>>().WithAll<PlayerTag>())        
        {
            direction.ValueRW.Value = currentInput;
        }
    }
}