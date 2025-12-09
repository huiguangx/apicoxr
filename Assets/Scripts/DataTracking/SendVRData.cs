using UnityEngine;

namespace DataTracking
{
    [System.Serializable]
    public class SendVRData
    {
        public string state = "NORMAL";      // 新增
        public int battery = 1;              // 新增

        public HeadInfo head;
        public ControllerInfo left;
        public ControllerInfo right;
        public long timestamp;

        public SendVRData()
        {
            head = new HeadInfo();
            left = new ControllerInfo();
            right = new ControllerInfo();
            timestamp = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }

    [System.Serializable]
    public class HeadInfo
    {
        public Vector3Data position;
        public QuaternionData rotation;
        public Vector4Data linearVelocity;      // 改为 Vector4Data（含 w）
        public Vector4Data angularVelocity;     // 改为 Vector4Data（含 w）

        public HeadInfo()
        {
            position = new Vector3Data();
            rotation = new QuaternionData();
            linearVelocity = new Vector4Data(); // 默认 (0,0,0,0)
            angularVelocity = new Vector4Data();
        }
    }

    [System.Serializable]
    public class ControllerInfo
    {
        public Vector3Data position;
        public QuaternionData rotation;
        public Vector4Data linearVelocity;      // 改为 Vector4Data
        public Vector4Data angularVelocity;     // 改为 Vector4Data
        public ButtonState[] button;
        public float[] axes;                    // 新增 axes: float[4]

        public ControllerInfo()
        {
            position = new Vector3Data();
            rotation = new QuaternionData();
            linearVelocity = new Vector4Data();
            angularVelocity = new Vector4Data();
            button = new ButtonState[7];
            for (int i = 0; i < button.Length; i++)
            {
                button[i] = new ButtonState();
            }
            axes = new float[4] { 0f, 0f, 0f, 0f }; // 默认 [0,0,0,0]
        }
    }

    // --- 保留原有 Vector3Data 和 QuaternionData ---
    [System.Serializable]
    public class Vector3Data
    {
        public float x, y, z;
        public Vector3Data() { }
        public Vector3Data(Vector3 v) { x = v.x; y = v.y; z = v.z; }
        public Vector3 ToVector3() => new Vector3(x, y, z);
    }

    [System.Serializable]
    public class QuaternionData
    {
        public float x, y, z, w;
        public QuaternionData() { }
        public QuaternionData(Quaternion q) { x = q.x; y = q.y; z = q.z; w = q.w; }
        public Quaternion ToQuaternion() => new Quaternion(x, y, z, w);
    }

    // --- 新增：用于 velocity（含 w 字段，即使不用）---
    [System.Serializable]
    public class Vector4Data
    {
        public float x, y, z, w;

        public Vector4Data() 
        {
            x = y = z = w = 0f;
        }

        // 可选：从 Vector3 构造（w=0）
        public Vector4Data(Vector3 v)
        {
            x = v.x;
            y = v.y;
            z = v.z;
            w = 0f;
        }

        public Vector4 ToVector4() => new Vector4(x, y, z, w);
    }

    [System.Serializable]
    public class ButtonState
    {
        public float value = 0f;
        public bool pressed = false;
        public bool touched = false;

        public ButtonState() { }
    }
}