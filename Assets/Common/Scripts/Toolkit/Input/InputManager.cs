using OctoberStudio.UI;
using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace OctoberStudio.Input
{
    /// <summary>
    /// 输入管理器，负责处理玩家输入（UI摇杆、键盘、手柄）的切换和输入值的读取
    /// </summary>
    public class InputManager : MonoBehaviour, IInputManager
    {
        private static InputManager instance;  // 单例实例

        [Header("References")]
        [SerializeField] HighlightsParentBehavior highlightsParent;  // 高亮提示父对象，用于显示按键提示
        public HighlightsParentBehavior Highlights => highlightsParent;

        private InputAsset inputAsset;          // Unity Input System 生成的输入资产
        public InputAsset InputAsset => inputAsset;

        private InputSave save;                 // 输入相关的存档

        public InputType ActiveInput { get => save.ActiveInput; private set => save.ActiveInput = value; }  // 当前激活的输入类型
        public Vector2 MovementValue { get; private set; }  // 当前移动输入值（归一化的方向向量）

        public JoystickBehavior Joystick { get; private set; }  // 当前注册的UI摇杆

        public event UnityAction<InputType, InputType> onInputChanged;  // 输入类型改变时触发的事件，参数为（之前类型，新类型）

        private void Awake()
        {
            // 单例模式，确保全局只有一个实例
            if (instance != null)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;

            DontDestroyOnLoad(gameObject);  // 场景切换时不销毁

            GameController.RegisterInputManager(this);  // 向游戏控制器注册当前输入管理器

            inputAsset = new InputAsset();   // 创建输入资产实例

            Init();  // 初始化
        }

        private void OnEnable()
        {
            inputAsset.Enable();  // 启用输入系统
        }

        private void OnDisable()
        {
            if (inputAsset != null) inputAsset.Disable();  // 禁用输入系统
        }

        /// <summary>
        /// 初始化：加载存档，根据当前连接设备设置默认输入类型，注册手柄检测事件
        /// </summary>
        public void Init()
        {
            save = GameController.SaveManager.GetSave<InputSave>("Input");

            if (Gamepad.current != null)  // 如果当前有手柄连接，默认使用手柄
            {
                ActiveInput = InputType.Gamepad;
            }
            else
            {
                ActiveInput = InputType.UIJoystick;  // 否则使用UI摇杆
            }

            inputAsset.GamepadDetection.Detection.performed += GamepadDetection;  // 监听手柄检测事件
        }

        private void Update()
        {
            // 键盘检测：当前输入不是键盘，且键盘有更新且不是默认状态时，切换到键盘输入
            if (ActiveInput != InputType.Keyboard && Keyboard.current != null && Keyboard.current.wasUpdatedThisFrame && !Keyboard.current.CheckStateIsAtDefaultIgnoringNoise())
            {
                Debug.Log("Switching To Keyboard");

                var prevInput = ActiveInput;
                ActiveInput = InputType.Keyboard;

                if (Joystick != null) Joystick.Disable();  // 如果有UI摇杆，禁用

                highlightsParent.EnableArrows();  // 显示键盘箭头提示

                onInputChanged?.Invoke(prevInput, InputType.Keyboard);
            }

            // UI摇杆检测：当满足以下任一条件时切换到UI摇杆：
            // - 当前不是UI摇杆且鼠标左键按下
            // - 当前是手柄但手柄已断开
            // - 触摸屏有更新（触摸操作）
            if (ActiveInput != InputType.UIJoystick &&
                (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame ||
                ActiveInput == InputType.Gamepad && Gamepad.current == null ||
                Touchscreen.current != null && Touchscreen.current.wasUpdatedThisFrame))
            {
                Debug.Log("Switching To UI Joystick");

                var prevInput = ActiveInput;
                ActiveInput = InputType.UIJoystick;

                if (Joystick != null) Joystick.Enable();  // 启用UI摇杆

                highlightsParent.DisableArrows();  // 隐藏键盘箭头提示

                onInputChanged?.Invoke(prevInput, InputType.UIJoystick);
            }

            // 根据当前输入类型获取移动输入值
            if (ActiveInput == InputType.UIJoystick && Joystick != null)
            {
                MovementValue = Joystick.Value;  // UI摇杆输入
            }
            else
            {
                MovementValue = inputAsset.Gameplay.Movement.ReadValue<Vector2>();  // 键盘或手柄输入
            }
        }

        /// <summary>
        /// 手柄检测回调，当手柄连接时自动切换到手柄输入
        /// </summary>
        private void GamepadDetection(InputAction.CallbackContext obj)
        {
            if (ActiveInput != InputType.Gamepad)
            {
                Debug.Log("Switching To Gamepad");

                var prevInput = ActiveInput;
                ActiveInput = InputType.Gamepad;

                if (Joystick != null) Joystick.Disable();

                highlightsParent.EnableArrows();

                onInputChanged?.Invoke(prevInput, InputType.Gamepad);
            }
        }

        /// <summary>
        /// 注册UI摇杆，并依据当前输入类型决定是否启用
        /// </summary>
        /// <param name="joystick">UI摇杆组件</param>
        public void RegisterJoystick(JoystickBehavior joystick)
        {
            Joystick = joystick;

            if (ActiveInput == InputType.UIJoystick)
            {
                joystick.Enable();
            }
            else
            {
                joystick.Disable();
            }
        }

        /// <summary>
        /// 移除已注册的UI摇杆引用
        /// </summary>
        public void RemoveJoystick()
        {
            Joystick = null;
        }
    }

    /// <summary>
    /// 输入类型枚举
    /// </summary>
    public enum InputType
    {
        UIJoystick = 1,   // UI摇杆（触摸屏或鼠标模拟）
        Keyboard = 2,     // 键盘
        Gamepad = 4,      // 游戏手柄
    }
}