using UnityEngine;
using UnityEngine.InputSystem;
// ButtonControl / KeyControl 在 Controls 子命名空间里，不在 UnityEngine.InputSystem 根下
using UnityEngine.InputSystem.Controls;

namespace DuduAdventure.Player
{
    /// <summary>
    /// 设备输入源（新 Input System）- 绑定到一个具体的手柄或键盘，本地多人用
    /// </summary>
    /// <remarks>
    /// 为什么不用 PlayerInput + .inputactions 资产：
    /// 那套流程要额外维护一个动作资产文件，改按键得开可视化编辑器，
    /// 而且和 PlayerInputManager 的自动绑定耦合较深，出问题很难在代码里查。
    /// 本地同屏的需求其实很简单——"这个角色只听这一个设备"——
    /// 直接轮询设备上的按键更直白，全部逻辑都在这一个文件里，方便一个人维护。
    ///
    /// 关键约定：这个组件必须**预先挂在角色 Prefab 上**，
    /// 由 PlayerJoinManager 在生成角色后调用 Bind() 绑定设备。
    /// 原因见 PlayerInputSourceResolver.Resolve 的注释。
    /// </remarks>
    [DisallowMultipleComponent]
    public class DeviceInputSource : MonoBehaviour, IPlayerInputSource
    {
        #region Inspector 配置

        [Header("手柄设置")]
        [Tooltip("摇杆死区（小于这个值视为没推摇杆，防止摇杆漂移导致角色自己走）")]
        [SerializeField] private float _stickDeadzone = 0.2f;

        #endregion

        #region 运行时状态

        // 绑定的手柄（键盘玩家时为 null）
        private Gamepad _gamepad;

        // 绑定的键盘（手柄玩家时为 null）
        private Keyboard _keyboard;

        #endregion

        #region 公共属性

        /// <summary>
        /// 当前绑定的设备（未绑定时为 null）
        /// </summary>
        public InputDevice Device { get; private set; }

        /// <summary>
        /// 设备是否仍然可用（拔掉手柄后会变 false）
        /// </summary>
        public bool IsDeviceConnected => Device != null && Device.added;

        /// <summary>
        /// 绑定设备的可读名称，用于 UI 显示"2P：Xbox 手柄"
        /// </summary>
        public string DeviceDisplayName
        {
            get
            {
                if (Device == null) return "未绑定";
                if (_keyboard != null) return "键盘";
                return Device.displayName;
            }
        }

        #endregion

        #region 绑定管理

        /// <summary>
        /// 把这个角色绑定到指定设备
        /// </summary>
        public void Bind(InputDevice device)
        {
            Device = device;
            _gamepad = device as Gamepad;
            _keyboard = device as Keyboard;

            if (_gamepad == null && _keyboard == null)
            {
                Debug.LogError(
                    $"[DeviceInputSource] {gameObject.name} 绑定了不支持的设备类型：{device?.GetType().Name}。" +
                    "目前只支持 Gamepad 和 Keyboard。");
            }
        }

        /// <summary>
        /// 解除绑定（玩家退出或手柄拔掉时调用）
        /// </summary>
        public void Unbind()
        {
            Device = null;
            _gamepad = null;
            _keyboard = null;
        }

        #endregion

        #region IPlayerInputSource 实现

        public float Horizontal
        {
            get
            {
                if (_gamepad != null)
                {
                    // 十字键优先：按下十字键就是明确的满速指令
                    if (_gamepad.dpad.left.isPressed) return -1f;
                    if (_gamepad.dpad.right.isPressed) return 1f;

                    // 摇杆走模拟量，轻推就慢走，这是手柄该有的手感
                    float stickX = _gamepad.leftStick.x.ReadValue();
                    return Mathf.Abs(stickX) < _stickDeadzone ? 0f : stickX;
                }

                if (_keyboard != null)
                {
                    float value = 0f;
                    if (_keyboard.aKey.isPressed || _keyboard.leftArrowKey.isPressed) value -= 1f;
                    if (_keyboard.dKey.isPressed || _keyboard.rightArrowKey.isPressed) value += 1f;
                    return value;
                }

                return 0f;
            }
        }

        public float Vertical
        {
            get
            {
                if (_gamepad != null)
                {
                    // 十字键优先
                    if (_gamepad.dpad.down.isPressed) return -1f;
                    if (_gamepad.dpad.up.isPressed) return 1f;

                    // 摇杆
                    float stickY = _gamepad.leftStick.y.ReadValue();
                    return Mathf.Abs(stickY) < _stickDeadzone ? 0f : stickY;
                }

                if (_keyboard != null)
                {
                    float value = 0f;
                    if (_keyboard.sKey.isPressed || _keyboard.downArrowKey.isPressed) value -= 1f;
                    if (_keyboard.wKey.isPressed || _keyboard.upArrowKey.isPressed) value += 1f;
                    return value;
                }

                return 0f;
            }
        }

        // 跳跃：手柄 A / 键盘空格
        public bool JumpPressed => ReadPressed(g => g.buttonSouth, k => k.spaceKey);

        public bool JumpHeld => ReadHeld(g => g.buttonSouth, k => k.spaceKey);

        public bool JumpReleased => ReadReleased(g => g.buttonSouth, k => k.spaceKey);

        // 攻击：手柄 X / 键盘 J
        public bool AttackPressed => ReadPressed(g => g.buttonWest, k => k.jKey);

        // 冲刺：手柄 B 或 RB / 键盘 K
        public bool DashPressed =>
            ReadPressed(g => g.buttonEast, k => k.kKey) ||
            ReadPressed(g => g.rightShoulder, k => k.leftShiftKey);

        // 大招：手柄 Y / 键盘 L
        public bool SpecialPressed => ReadPressed(g => g.buttonNorth, k => k.lKey);

        #endregion

        #region 按键读取辅助

        // 下面三个辅助方法把"手柄还是键盘"的分支收敛到一处，
        // 避免每个按键属性都重复写一遍 null 检查。
        // 用委托选择具体按键，调用处只需声明"手柄上是哪个键、键盘上是哪个键"。

        private bool ReadPressed(
            System.Func<Gamepad, ButtonControl> gamepadButton,
            System.Func<Keyboard, KeyControl> keyboardKey)
        {
            if (_gamepad != null) return gamepadButton(_gamepad).wasPressedThisFrame;
            if (_keyboard != null) return keyboardKey(_keyboard).wasPressedThisFrame;
            return false;
        }

        private bool ReadHeld(
            System.Func<Gamepad, ButtonControl> gamepadButton,
            System.Func<Keyboard, KeyControl> keyboardKey)
        {
            if (_gamepad != null) return gamepadButton(_gamepad).isPressed;
            if (_keyboard != null) return keyboardKey(_keyboard).isPressed;
            return false;
        }

        private bool ReadReleased(
            System.Func<Gamepad, ButtonControl> gamepadButton,
            System.Func<Keyboard, KeyControl> keyboardKey)
        {
            if (_gamepad != null) return gamepadButton(_gamepad).wasReleasedThisFrame;
            if (_keyboard != null) return keyboardKey(_keyboard).wasReleasedThisFrame;
            return false;
        }

        #endregion
    }
}
