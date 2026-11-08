ECS幸存者项目学习记录

    子场景的好处：
        在子场景中创建游戏对象
    
    ECS里要使用3D collider,并且虽然是2D游戏（用3D制作），要设置一个小Z值,确保碰撞发生

    unity 对ECS模式的inspector的适配：右上角的运行模式，有专门用于ECS的局内视角
    
    创建非托管式的数据组件，与unity的job system和burst complier完全兼容， 继承IComponentData，意味着可以添加到ECS ；注意数据的分离，以及在运行时修改的数据会保留在对应的数据组件里--结束运行不会恢复修改前；

    移动的时候不能直接对position+=，这样是瞬间运动，碰撞不会发生，要对速度+=

    创建单例模式摄像机目标；

    问题一：对于输入Action生命周期的管理
        现象：PlayerTag、CharacterMoveDirection和移动系统都正常，但
        _input.Player.Move.ReadValue<Vector2>()始终为(0,0)，并且Move.enabled为false。

        排查：SurvivorsInput在OnCreate中调用Enable后，asset和Player map
        都曾经为true；进入OnUpdate时又变为false。开启Enter Play Mode Options
        中的Domain Reload后移动恢复，关闭Domain Reload后问题再次出现。

        原因：关闭Domain Reload时，Unity不会重新加载脚本域和静态运行状态。
        运行时创建的InputActionAsset不会像正常重新加载时一样自动重置，
        导致输入Action的生命周期状态残留为disabled。

        解决方案：
        1. 最简单可靠的方式是保持Domain Reload开启。
        2. 如果必须关闭Domain Reload，就要显式管理输入Action的生命周期：
           在OnCreate创建SurvivorsInput，在OnStartRunning中调用
           _input.asset.Enable()，在OnStopRunning中调用
           _input.asset.Disable()，并在OnDestroy中先Disable再Dispose。
        3. 不要只依赖OnCreate中的一次Enable；ECS系统可能停止后再次运行，
           每次OnStartRunning都应重新启用输入。


        
    
    