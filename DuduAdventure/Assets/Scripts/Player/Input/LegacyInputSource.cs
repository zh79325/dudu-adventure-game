using UnityEngine;

namespace DuduAdventure.Player
{
    /// <summary>
    /// 键盘输入源（老 Input 系统）- 单人调试与兜底用
    /// </summary>
    /// <remarks>
    /// 用老 Input 系统而不是新的，是为了让已有的灰盒测试场景一行都不改就能继续跑。
    /// Project Settings 里 Active Input Handling 已设为 Both，两套系统可以共存。
    ///
    /// 本地多人时不要用这个类：老 Input 是全局单例，无法区分是哪个键盘/手柄。
    /// </remarks>
    [DisallowMultipleComponent]
    public class LegacyInputSource : MonoBehaviour, IPlayerInputSource
    {
        [Header("按键映射")]
        [Tooltip("攻击键（鼠标左键始终有效）")]
        [SerializeField] private KeyCode _attackKey = KeyCode.J;

        [Tooltip("冲刺键（左 Shift 始终有效）")]
        [SerializeField] private KeyCode _dashKey = KeyCode.K;

        [Tooltip("大招键")]
        [SerializeField] private KeyCode _specialKey = KeyCode.L;

        // 水平输入用 GetAxisRaw 而不是 GetAxis，
        // 是因为动作游戏要"按下立刻满速"，不要 Unity 内置的平滑。
        // 加速手感由 PlayerController 的 SmoothDamp 统一负责。
        public float Horizontal => UnityEngine.Input.GetAxisRaw("Horizontal");

        public bool JumpPressed => UnityEngine.Input.GetButtonDown("Jump");

        public bool JumpHeld => UnityEngine.Input.GetButton("Jump");

        public bool JumpReleased => UnityEngine.Input.GetButtonUp("Jump");

        public bool AttackPressed =>
            UnityEngine.Input.GetMouseButtonDown(0) || UnityEngine.Input.GetKeyDown(_attackKey);

        public bool DashPressed =>
            UnityEngine.Input.GetKeyDown(KeyCode.LeftShift) || UnityEngine.Input.GetKeyDown(_dashKey);

        public bool SpecialPressed => UnityEngine.Input.GetKeyDown(_specialKey);
    }
}
