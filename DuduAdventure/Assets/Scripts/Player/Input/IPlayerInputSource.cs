using UnityEngine;

// 注意：命名空间故意保持在 DuduAdventure.Player，而不是 DuduAdventure.Player.Input。
// 如果命名空间里出现 Input 这一段，实现类里写 Input.GetKeyDown 时
// 编译器会把 Input 解析成命名空间而不是 UnityEngine.Input，直接编译报错。
// 文件放在 Input/ 目录只是为了归类，Unity 不要求目录和命名空间一致。
namespace DuduAdventure.Player
{
    /// <summary>
    /// 玩家输入源 - 把"谁在按键"和"角色怎么动"彻底解耦
    /// </summary>
    /// <remarks>
    /// 为什么需要这一层：
    /// 单人游戏里可以直接 Input.GetAxisRaw("Horizontal")，因为全局只有一个玩家。
    /// 一旦要本地多人（沙发同乐），全局 Input 就是致命问题——4 个角色会同时响应同一个按键。
    /// 所以每个角色身上挂一个自己的输入源组件，角色只问"我的输入源现在是什么值"，
    /// 不关心背后是键盘、1 号手柄还是 4 号手柄。
    ///
    /// 实现类：
    /// - LegacyInputSource：老 Input 系统的键盘，单人调试用，保证已有场景不改也能跑
    /// - DeviceInputSource：新 Input System，绑定到具体设备，本地多人用
    /// </remarks>
    public interface IPlayerInputSource
    {
        /// <summary>
        /// 水平输入（-1 = 左, 0 = 松开, 1 = 右）
        /// 手柄摇杆会给出中间值，角色移动速度会跟着变，这是有意的
        /// </summary>
        float Horizontal { get; }

        /// <summary>
        /// 本帧刚按下跳跃键
        /// </summary>
        bool JumpPressed { get; }

        /// <summary>
        /// 跳跃键正被按住
        /// </summary>
        bool JumpHeld { get; }

        /// <summary>
        /// 本帧刚松开跳跃键（用于短按小跳）
        /// </summary>
        bool JumpReleased { get; }

        /// <summary>
        /// 本帧刚按下攻击键
        /// </summary>
        bool AttackPressed { get; }

        /// <summary>
        /// 本帧刚按下冲刺键
        /// </summary>
        bool DashPressed { get; }

        /// <summary>
        /// 本帧刚按下大招键（各角色专属技，阶段 4 使用）
        /// </summary>
        bool SpecialPressed { get; }
    }

    /// <summary>
    /// 输入源查找工具
    /// </summary>
    public static class PlayerInputSourceResolver
    {
        /// <summary>
        /// 从角色身上找输入源；找不到就补一个键盘输入源
        /// </summary>
        /// <remarks>
        /// 兜底存在的意义：手工在场景里摆的调试角色（比如 Level1 的灰盒 Player）
        /// 不用改场景也能继续跑。
        ///
        /// 但要注意顺序陷阱：如果由 PlayerJoinManager 动态生成角色，
        /// 输入源必须**预先挂在 Prefab 上**。因为 Instantiate 的瞬间就会执行 Awake，
        /// 那时如果身上还没有输入源，这里就会兜底加一个 LegacyInputSource，
        /// 之后再 AddComponent&lt;DeviceInputSource&gt; 就变成两个输入源，
        /// 而角色缓存的是先加的那个键盘源 —— 4 个人一起按键会全体一起动。
        /// </remarks>
        public static IPlayerInputSource Resolve(GameObject owner)
        {
            var source = owner.GetComponent<IPlayerInputSource>();

            if (source == null)
            {
                Debug.LogWarning(
                    $"[PlayerInputSourceResolver] {owner.name} 身上没有输入源组件，" +
                    "已自动补一个 LegacyInputSource（键盘）。本地多人时请在 Prefab 上挂 DeviceInputSource。");

                source = owner.AddComponent<LegacyInputSource>();
            }

            return source;
        }
    }
}
