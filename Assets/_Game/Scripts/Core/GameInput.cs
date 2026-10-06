using UnityEngine;
using UnityEngine.InputSystem;

namespace LightsOut
{
    /// <summary>
    /// Single read point for player input: on-screen joystick (phones), keyboard WASD/arrows and gamepad (PC testing).
    /// </summary>
    public static class GameInput
    {
        /// <summary>Written by VirtualJoystick.</summary>
        public static Vector2 TouchMove;

        /// <summary>Set by the -lo-bot command line flag: the local player wanders by itself.</summary>
        public static bool BotMode;

        static Vector2 _botDir;
        static float _botNextTurn;

        public static Vector2 Move
        {
            get
            {
                if (BotMode) return BotMove();

                Vector2 v = TouchMove;
                var kb = Keyboard.current;
                if (kb != null)
                {
                    if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) v.x -= 1;
                    if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) v.x += 1;
                    if (kb.sKey.isPressed || kb.downArrowKey.isPressed) v.y -= 1;
                    if (kb.wKey.isPressed || kb.upArrowKey.isPressed) v.y += 1;
                }
                var pad = Gamepad.current;
                if (pad != null) v += pad.leftStick.ReadValue();
                return Vector2.ClampMagnitude(v, 1f);
            }
        }

        static Vector2 BotMove()
        {
            if (Time.time >= _botNextTurn)
            {
                _botDir = Random.insideUnitCircle.normalized;
                _botNextTurn = Time.time + Random.Range(0.6f, 2f);
            }
            return _botDir;
        }
    }
}
