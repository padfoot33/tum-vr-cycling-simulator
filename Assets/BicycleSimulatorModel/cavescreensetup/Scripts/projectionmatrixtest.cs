using UnityEngine;

[ExecuteInEditMode]
public class ExampleClass : MonoBehaviour
{
    // Variables for the first camera
    [Header("Front Camera")]
    public float left1 = -0.2F;
    public float right1 = 0.2F;
    public float top1 = 0.2F;
    public float bottom1 = -0.2F;
    public float near1 = 1.7F;
    public float far1 = 200F;
    public Camera camFront_LeftEye;
    public Camera camFront_RightEye;

    // Variables for the second camera
    [Header("Right Camera")]
    public float left2 = -0.2F;
    public float right2 = 0.2F;
    public float top2 = 0.2F;
    public float bottom2 = -0.2F;
    public float near2 = 1.7F;
    public float far2 = 200F;
    public Camera camRight_RightEye;
    public Camera camRight_LeftEye;


    // Variables for the third camera
    [Header("Left Camera")]
    public float left3 = -0.2F;
    public float right3 = 0.2F;
    public float top3 = 0.2F;
    public float bottom3 = -0.2F;
    public float near3 = 1.7F;
    public float far3 = 200F;
    public Camera camLeft_RightEye;
    public Camera camLeft_LeftEye;

    // Variables for the third camera
    [Header("Floor Camera")]
    public float left4 = -0.2F;
    public float right4 = 0.2F;
    public float top4 = 0.2F;
    public float bottom4 = -0.2F;
    public float near4 = 1.7F;
    public float far4 = 200F;
    public Camera camFloor_RightEye;
    public Camera camFloor_LeftEye;
    

    void LateUpdate()
    {

        // FRONT CAMERA
        if (camFront_LeftEye != null)
        {
            Matrix4x4 m1 = PerspectiveOffCenter(left1, right1, bottom1, top1, near1, far1);
            camFront_LeftEye.projectionMatrix = m1;
        }
        if (camFront_RightEye != null)
        {
            Matrix4x4 m2 = PerspectiveOffCenter(left1, right1, bottom1, top1, near1, far1);
            camFront_RightEye.projectionMatrix = m2;
        }



        // LEFT CAMERA
        if (camLeft_RightEye != null)
        {
            Matrix4x4 m4 = PerspectiveOffCenter(left3, right3, bottom3, top3, near3, far3);
            camLeft_RightEye.projectionMatrix = m4;
        }
        if (camLeft_LeftEye != null)
        {
            Matrix4x4 m5 = PerspectiveOffCenter(left3, right3, bottom3, top3, near3, far3);
            camLeft_LeftEye.projectionMatrix = m5;
        }


        // RIGHT CAMERA
        if(camRight_RightEye != null)
        {
            Matrix4x4 m6 = PerspectiveOffCenter(left2, right2, bottom2, top2, near2, far2);
            camRight_RightEye.projectionMatrix = m6;
        }
        if (camRight_LeftEye != null)
        {
            Matrix4x4 m7 = PerspectiveOffCenter(left2, right2, bottom2, top2, near2, far2);
            camRight_LeftEye.projectionMatrix = m7;
        }

        // Floor CAMERA
        if(camFloor_RightEye != null)
        {
            Matrix4x4 m8 = PerspectiveOffCenter(left4, right4, bottom4, top4, near4, far4);
            camFloor_RightEye.projectionMatrix = m8;
        }
        if (camFloor_LeftEye != null)
        {
            Matrix4x4 m9 = PerspectiveOffCenter(left4, right4, bottom4, top4, near4, far4);
            camFloor_LeftEye.projectionMatrix = m9;
        }
    }

    static Matrix4x4 PerspectiveOffCenter(float left, float right, float bottom, float top, float near, float far)
    {
        float x = 2.0F * near / (right - left);
        float y = 2.0F * near / (top - bottom);
        float a = (right + left) / (right - left);
        float b = (top + bottom) / (top - bottom);
        float c = -(far + near) / (far - near);
        float d = -(2.0F * far * near) / (far - near);
        float e = -1.0F;
        Matrix4x4 m = new Matrix4x4();
        m[0, 0] = x;
        m[0, 1] = 0;
        m[0, 2] = a;
        m[0, 3] = 0;
        m[1, 0] = 0;
        m[1, 1] = y;
        m[1, 2] = b;
        m[1, 3] = 0;
        m[2, 0] = 0;
        m[2, 1] = 0;
        m[2, 2] = c;
        m[2, 3] = d;
        m[3, 0] = 0;
        m[3, 1] = 0;
        m[3, 2] = e;
        m[3, 3] = 0;
        return m;
    }
}
