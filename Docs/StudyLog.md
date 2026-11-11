ECS幸存者项目学习记录

    子场景的好处：
        在子场景中创建游戏对象
    
    ECS里要使用3D collider,并且虽然是2D游戏（用3D制作），要设置一个小Z值,确保碰撞发生

    unity 对ECS模式的inspector的适配：右上角的运行模式，有专门用于ECS的局内视角
    
    创建非托管式的数据组件，与unity的job system和burst complier完全兼容， 继承IComponentData，意味着可以添加到ECS ；注意数据的分离，以及在运行时修改的数据会保留在对应的数据组件里--结束运行不会恢复修改前；

    移动的时候不能直接对position+=，这样是瞬间运动，碰撞不会发生，要对速度+=

    创建单例模式摄像机目标；
    对于rigid body的interpolate选项：Interpolate：使用前两次物理更新后的已知位置，在两者之间做插值。Extrapolate：使用上一次物理更新的位置，并依据当前速度预测下一次物理更新后的位置。

    用job来处理敌人移动到玩家的逻辑，减少主线程的压力，提高性能





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

问题二：`用shader写，代替传统的Sprite Renderer + Animator 的逐帧动画`
        - 把 Sprite Sheet 动画的“播放控制”从 CPU 和 Animator 手里抢过来，全部塞进 GPU 的顶点/片元阶段完成。
        - 把逐帧动画从“对象级、CPU 驱动”变成“数据级、GPU 驱动”，在需要大量同屏动画精灵或 ECS 架构时，这是传统 Sprite Renderer + Animator 很难达到的

    好处：
        1. 彻底绕开 Animator 和动画剪辑
            逐帧播放、多动画切换、水平翻转这些原本需要 Animator Controller、Animation Clip、Sprite Renderer 的 flipX 来驱动的东西，现在只需要在材质或顶点数据里传几个参数（当前帧索引、动画起始帧、总帧数、速度、是否翻转），Shader 自己算 UV 偏移。这意味着：不需要为每套动画建剪辑、不需要 Animator 状态机、不需要每帧改 Sprite 引用，动画数据变成了纯数值，极易由代码或 ECS 系统驱动。

        2. 天然适配 ECS / GPU Instancing
            这是最关键的一点。Unity 的 Animator 和 Sprite Renderer 是 GameObject 体系的东西，在 ECS 或大量同屏精灵的场景里几乎不可用——每个实体挂 Animator 会让性能崩掉，Sprite Renderer 也不支持高效的 instancing。而你的 Shader 把“当前帧”变成一个 per-instance 的材质属性或顶点属性，几千个精灵可以一次 Draw Call 提交，每个实例各自播放不同帧、不同动画、不同染色和透明度。这是传统 Sprite Renderer 做不到的。

        3. 颜色染色和透明裁剪在 GPU 上完成
            染色（tint）和 alpha 裁剪（clip）放在片元着色器里，意味着不需要为不同颜色生成不同贴图，也不需要 CPU 逐像素处理。透明裁剪用 clip(alpha - threshold) 还能顺便避免半透明排序问题，适合大量精灵叠加的场景。

        4. 减少 CPU 开销和状态切换
            逐帧动画不再需要 CPU 每帧修改 Sprite 的 sprite 引用或 UV，CPU 只负责更新一个帧索引数值。对于同屏几百上千个动画精灵，这能显著降低主线程的 Animator 更新、Sprite 切换和 SetPass 开销。

        5. 动画逻辑与渲染解耦
            帧索引、动画段、翻转、染色都是参数，动画系统可以由你自己的逻辑（甚至 ECS Job/System）驱动，而不是被 Animator 绑死。这让网络同步、回放、批量控制都变得简单。
        
    代价：
            灵活性换性能：动画段、帧数、翻转逻辑都写死在 Shader 参数里，改动画结构要改参数或 Shader，不如 Animator 可视化编辑方便。

            调试成本：UV 算错、帧边界偏移、图集 padding 导致的采样溢出，在 Shader 里排查比在 Sprite Editor 里麻烦。

            透明裁剪的锯齿：clip 是硬裁剪，边缘会有锯齿，需要配合 alpha 测试阈值或 MSAA/后处理处理。

            图集要求：Sprite Sheet 的帧布局必须规整（等宽等高、无旋转打包），否则 UV 计算会复杂很多。
    
    