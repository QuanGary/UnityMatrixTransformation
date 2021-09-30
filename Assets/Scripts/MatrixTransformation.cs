using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using static UnityEngine.Mathf;

public class MatrixTransformation : MonoBehaviour
{
    public GameObject cube;

    public TMP_InputField inputRotationAngle;
    public TMP_InputField inputReflectionAngle;
    public TMP_InputField inputHorizontalShear;
    public TMP_InputField inputVerticalShear;


    public AnimationCurve curveX0;
    public AnimationCurve curveX1;
    public AnimationCurve curveY0;
    public AnimationCurve curveY1;
    public AnimationCurve curveAngle;

    public TMP_InputField inputX0;
    public TMP_InputField inputX1;
    public TMP_InputField inputY0;
    public TMP_InputField inputY1;
    public TMP_InputField inputAngle;

    private float x0;
    private float x1;
    private float y0;
    private float y1;
    private float angle;

    private List<AnimationCurve> _curves;
    private bool _animationPlaying;
    private float _animationTime;
    private float _maxAnimationTime;
    private Queue<RotationInterval> _rotationIntervals; // store the intervals that are using rotation
    private Queue<RotationInterval> _rotationIntervalsCopy;

    private Mesh _mesh;
    private Vector3[] _vertices;


    // Camera stuff
    [FormerlySerializedAs("camera")] [SerializeField]
    private Camera cam;

    private float _camDistToCube;
    private Vector3 _prevMousePos;

    // Constants
    private const int InitialWaitTime = 2;


    // Start is called before the first frame update
    void Start()
    {
        curveX0 = new AnimationCurve();
        curveX1 = new AnimationCurve();
        curveY0 = new AnimationCurve();
        curveY1 = new AnimationCurve();
        _curves = new List<AnimationCurve> { curveX0, curveX1, curveY0, curveY1 }; // If more curves, add them here

        _rotationIntervals = new Queue<RotationInterval>();
        _rotationIntervalsCopy = new Queue<RotationInterval>();
        SetText(inputX0, 1, "none");
        SetText(inputX1, 0, "none");
        SetText(inputY0, 0, "none");
        SetText(inputY1, 1, "none");
        inputRotationAngle.text = "";
        inputReflectionAngle.text = "";
        inputHorizontalShear.text = "";
        inputVerticalShear.text = "";

        _mesh = cube.GetComponent<MeshFilter>().mesh;
        _vertices = _mesh.vertices;

        // Add listeners for all input fields to update their values
        inputX0.onValueChanged.AddListener(delegate { ApplyCustomTransformation(); });
        inputX1.onValueChanged.AddListener(delegate { ApplyCustomTransformation(); });
        inputY0.onValueChanged.AddListener(delegate { ApplyCustomTransformation(); });
        inputY1.onValueChanged.AddListener(delegate { ApplyCustomTransformation(); });


        _camDistToCube = Vector3.Distance(cam.transform.position, cube.transform.position);


        CreateAnimationCurves(); // Create animation curves on start
    }

    /// <summary>
    /// Create keyframes for the 4 curves (x0,x1,y0,y1) which are the values
    /// in the 2D matrix
    /// </summary>
    private void CreateAnimationCurves()
    {
        _animationPlaying = false;
        _animationTime = 0f;

        curveX0.AddKey(0, 1);
        curveX1.AddKey(0, 0);
        curveY0.AddKey(0, 0);
        curveY1.AddKey(0, 1);
        ////////////////////////////////////////////////////////////
        // SCALE X
        ScaleX(1f, 1.5f, 1.5f, 0);
        ScaleX(1.5f, 1f, 1.5f, 0.5f);
        // SCALE Y
        ScaleY(1f, 1.5f, 1.5f, InitialWaitTime);
        ScaleY(1.5f, 1f, 1.5f, 0.5f);
        
        ////////////////////////////////////////////////////////////
        // SHEAR X
        ShearX(0, -0.5f, 1f, InitialWaitTime);
        ShearX(-0.5f, 0.5f, 2f, 0); // set iniWaitTime to 0 to start immediately
        ShearX(0.5f, 0, 1f, 0);
        // SHEAR Y
        ShearY(0, -0.5f, 1f, InitialWaitTime);
        ShearY(-0.5f, 0.5f, 2f, 0);
        ShearY(0.5f, 0, 1f, 0);
        
        ////////////////////////////////////////////////////////////
        // REFLECT ACROSS Y
        ScaleX(1f, -1f, 1.5f, InitialWaitTime);
        ScaleX(-1f, 1f, 1.5f, 0.5f);
        // REFLECT ACROSS X
        ScaleY(1f, -1f, 1.5f, InitialWaitTime);
        ScaleY(-1f, 1f, 1.5f, 0.5f);
        
        ////////////////////////////////////////////////////////////
        // ROTATION BY SHEAR (to 0.5)
        ShearX(0, -0.5f, 1.5f, InitialWaitTime);
        ShearY(0, 0.5f, 1.5f, 1);
        ShearX(-0.5f, 0, 0.5f, 1);
        ShearY(0.5f, 0, 0.5f, -0.5f); // set iniWaitTime to -0.5 to start concurrently with ShearX
        
        //(to 1.5)
        ShearX(0, -1f, 1.5f, InitialWaitTime);
        ShearY(0, 1f, 1.5f, 1);
        ShearX(-1f, 0f, 0.5f, 1);
        ShearY(1f, 0f, 0.5f, -0.5f);


        ////////////////////////////////////////////////////////////
        // SIN AND COS XFORMS
        SinTransform(0, 0.7854f, 2f, InitialWaitTime);
        SinTransform(0.7854f, 0, 2f, 0);

        CosTransform(0, 0.7854f, 2f, InitialWaitTime);
        CosTransform(0.7854f, 0, 2f, 0);

        ////////////////////////////////////////////////////////////
        // FULL ROTATION 0->2PI
        Rotation(0, 6.28f, 5, InitialWaitTime);
    }


    // Update is called once per frame
    void Update()
    {
        // MoveCamera();
        UseAnimationCurve();
        TransformMesh();
    }

    /// <summary>
    /// Press "Space" to start/reset the animation. The function evaluates
    /// the main curves (x0,x1,y0,y1) to apply transformation
    /// </summary>
    private void UseAnimationCurve()
    {
        if (Input.GetKeyDown("space")) // Press space to start playing the animation
        {
            _animationPlaying = true;
            _animationTime = 0f;
            _maxAnimationTime =
                GetLastKeyTime(); // Get max animation time by getting the time of the last key from all curves
            _rotationIntervalsCopy = new Queue<RotationInterval>(_rotationIntervals);
        }

        if (!_animationPlaying) return;
        if (inputX0.isFocused || inputX1.isFocused || inputY0.isFocused || inputY1.isFocused ||
            _animationTime >= _maxAnimationTime)
        {
            _animationPlaying = false;
            _animationTime = 0f;
            return;
        }

        _animationTime += Time.deltaTime;


        var usingRotation = "none";
        if (_rotationIntervalsCopy.Count > 0)
        {
            RotationInterval rt = _rotationIntervalsCopy.Peek();
            if (_animationTime <= rt.EndTime)
            {
                usingRotation = _animationTime >= rt.StartTime ? rt.Type : "none";
            }
            else
            {
                _rotationIntervalsCopy.Dequeue();
            }
        }

        SetText(inputX0, curveX0.Evaluate(_animationTime), usingRotation);
        SetText(inputX1, curveX1.Evaluate(_animationTime), usingRotation);
        SetText(inputY0, curveY0.Evaluate(_animationTime), usingRotation);
        SetText(inputY1, curveY1.Evaluate(_animationTime), usingRotation);
        SetText(inputAngle, curveAngle.Evaluate(_animationTime), usingRotation);
    }


    /// Scale the 2D face horizontally (by modifying x0)
    /// 
    /// <param name="startX"></param> The starting value for x0
    /// <param name="endX"></param> The ending value for x0
    /// <param name="animDuration"></param> Duration of animation
    /// <param name="iniWaitTime"></param>  Wait time before animation begins
    private void ScaleX(float startX, float endX, float animDuration, float iniWaitTime)
    {
        var anim = GetAnimationDuration(animDuration, iniWaitTime);
        var startTime = anim.StartTime;
        var endTime = anim.EndTime;

        curveX0.AddKey(startTime, startX);
        curveX0.AddKey(endTime, endX);
        SetCurveLinear(curveX0);
    }

    /// Scale the 2D face vertically (by modifying y1)
    /// 
    /// <param name="startY"></param> The starting value for y1
    /// <param name="endY"></param> The ending value for y1
    /// <param name="animDuration"></param> Duration of animation
    /// <param name="iniWaitTime"></param>  Wait time before animation begins
    private void ScaleY(float startY, float endY, float animDuration, float iniWaitTime)
    {
        var anim = GetAnimationDuration(animDuration, iniWaitTime);
        var startTime = anim.StartTime;
        var endTime = anim.EndTime;

        curveY1.AddKey(startTime, startY);
        curveY1.AddKey(endTime, endY);
        SetCurveLinear(curveY1);
    }

    /// Shear the 2D face horizontally (by modifying y0)
    /// 
    /// <param name="startX"></param> The starting value for y0
    /// <param name="endX"></param> The ending value for y0
    /// <param name="animDuration"></param> Duration of animation
    /// <param name="iniWaitTime"></param>  Wait time before animation begins
    private void ShearX(float startX, float endX, float animDuration, float iniWaitTime)
    {
        var anim = GetAnimationDuration(animDuration, iniWaitTime);
        var startTime = anim.StartTime;
        var endTime = anim.EndTime;

        curveY0.AddKey(startTime, startX);
        curveY0.AddKey(endTime, endX);
        SetCurveLinear(curveY0);
    }

    /// Shear the 2D face vertically (by modifying x1)
    /// 
    /// <param name="startY"></param> The starting value for x1
    /// <param name="endY"></param> The ending value for x1
    /// <param name="animDuration"></param> Duration of animation
    /// <param name="iniWaitTime"></param>  Wait time before animation begins
    private void ShearY(float startY, float endY, float animDuration, float iniWaitTime)
    {
        var anim = GetAnimationDuration(animDuration, iniWaitTime);
        var startTime = anim.StartTime;
        var endTime = anim.EndTime;

        curveX1.AddKey(startTime, startY);
        curveX1.AddKey(endTime, endY);
        SetCurveLinear(curveX1);
    }

    /// Apply Sine transformation about an angle(by modifying x1 and y0)
    /// 
    /// <param name="fromRadAngle"></param> The initial angle (in radians)
    /// <param name="toRadAngle"></param> The ending angle (in radians)
    /// <param name="animDuration"></param> Duration of animation
    /// <param name="iniWaitTime"></param>  Wait time before animation begins
    private void SinTransform(float fromRadAngle, float toRadAngle, float animDuration, float iniWaitTime)
    {
        var anim = GetAnimationDuration(animDuration, iniWaitTime);
        var startTime = anim.StartTime;
        var endTime = anim.EndTime;

        var sinFromAngle = Sin(fromRadAngle);
        var sinToAngle = Sin(toRadAngle);

        ShearY(sinFromAngle, sinToAngle, animDuration, iniWaitTime);
        ShearX(sinFromAngle, -sinToAngle, animDuration, -animDuration);
        curveAngle.AddKey(startTime, fromRadAngle);
        curveAngle.AddKey(endTime, toRadAngle);


        RotationInterval rt = new RotationInterval(startTime, endTime, "sin");
        _rotationIntervals.Enqueue(rt);
    }

    /// Apply Cosine transformation about an angle (by modifying x0 and y1)
    /// 
    /// <param name="fromRadAngle"></param> The initial angle (in radians)
    /// <param name="toRadAngle"></param> The ending angle (in radians)
    /// <param name="animDuration"></param> Duration of animation
    /// <param name="iniWaitTime"></param>  Wait time before animation begins
    private void CosTransform(float fromRadAngle, float toRadAngle, float animDuration, float iniWaitTime)
    {
        var anim = GetAnimationDuration(animDuration, iniWaitTime);
        var startTime = anim.StartTime;
        var endTime = anim.EndTime;

        var cosFromAngle = Cos(fromRadAngle);
        var cosToAngle = Cos(toRadAngle);

        // To specified angle
        ScaleX(cosFromAngle, cosToAngle, animDuration, iniWaitTime);
        ScaleY(cosFromAngle, cosToAngle, animDuration, -animDuration);
        curveAngle.AddKey(startTime, fromRadAngle);
        curveAngle.AddKey(endTime, toRadAngle);

        RotationInterval rt = new RotationInterval(startTime, endTime, "cos");
        _rotationIntervals.Enqueue(rt);
    }

    /// Apply full rotation around angle (in radians)
    /// 
    /// <param name="fromRadAngle"></param> The initial angle (in radians)
    /// <param name="toRadAngle"></param> The ending angle (in radians)
    /// <param name="animDuration"></param> Duration of animation
    /// <param name="iniWaitTime"></param>  Wait time before animation begins
    private void Rotation(float fromRadAngle, float toRadAngle, float animDuration, float iniWaitTime)
    {
        var anim = GetAnimationDuration(animDuration, iniWaitTime);
        var startTime = anim.StartTime;
        var endTime = anim.EndTime;

        var smallAngle = toRadAngle / 20;
        var smallDuration = animDuration / 20f;


        var sinFromAngle = Sin(fromRadAngle);
        var cosFromAngle = Cos(fromRadAngle);
        var sinToAngle = Sin(toRadAngle);
        var cosToAngle = Cos(toRadAngle);

        curveX0.AddKey(startTime, cosFromAngle);
        curveX1.AddKey(startTime, sinFromAngle);
        curveY0.AddKey(startTime, -sinFromAngle);
        curveY1.AddKey(startTime, cosFromAngle);
        curveAngle.AddKey(startTime, fromRadAngle);
        SmoothCurve(curveX0);
        SmoothCurve(curveX1);
        SmoothCurve(curveY0);
        SmoothCurve(curveY1);

        for (int i = 1; i <= 20; i++)
        {
            float time = startTime + i * smallDuration;
            float cosSmallAngle = Cos(smallAngle * i);
            float sinSmallAngle = Sin(smallAngle * i);
            curveX0.AddKey(time, cosSmallAngle);
            curveX1.AddKey(time, sinSmallAngle);
            curveY0.AddKey(time, -sinSmallAngle);
            curveY1.AddKey(time, cosSmallAngle);
            var smallAngleRadians = (sinSmallAngle > 0) ? Acos(cosSmallAngle) : -Acos(cosSmallAngle);
            if (smallAngleRadians < 0) smallAngleRadians += 6.28f;
            curveAngle.AddKey(time, smallAngleRadians);
        }

        curveX0.AddKey(endTime, cosToAngle);
        curveX1.AddKey(endTime, sinToAngle);
        curveY0.AddKey(endTime, -sinToAngle);
        curveY1.AddKey(endTime, cosToAngle);
        var angleRadian = (sinToAngle > 0) ? Acos(cosToAngle) : -Acos(cosToAngle);
        if (angleRadian < 0) angleRadian += 6.28f;
        curveAngle.AddKey(endTime, angleRadian);

        SmoothRotationCurve(curveX0);
        SmoothRotationCurve(curveX1);
        SmoothRotationCurve(curveY0);
        SmoothRotationCurve(curveY1);
        SetCurveLinear(curveAngle);

        RotationInterval rt = new RotationInterval(startTime, endTime, "both");
        _rotationIntervals.Enqueue(rt);
    }

    // private void Reflect(float toX, float toY, int duration)
    // {
    //     float lastKeyTime = GetLastKeyTime();
    //
    //     float startTime = lastKeyTime + PauseDuration;
    //     float firstRoundTrip = startTime + duration / 2f;
    //     float endTime = startTime + duration;
    //
    //     curvex0.AddKey(startTime, 1);
    //     curvex0.AddKey(firstRoundTrip, toX);
    //     curvex0.AddKey(endTime, 1);
    //     SmoothCurve(curvex0);
    //
    //     curvey1.AddKey(startTime, 1);
    //     curvey1.AddKey(firstRoundTrip, toY);
    //     curvey1.AddKey(endTime, 1);
    //     SmoothCurve(curvey1);
    // }


    ///////////////////////////////////////////////////////////////////////////////////////
    /// HELPER FUNCTIONS
    ///////////////////////////////////////////////////////////////////////////////////////
    private AnimationDuration GetAnimationDuration(float animDuration, float initialWaitTime)
    {
        var lastKeyTime = GetLastKeyTime();
        var startTime = lastKeyTime + initialWaitTime;
        var endTime = startTime + animDuration;

        return new AnimationDuration(startTime, endTime);
    }

    private float GetLastKeyTime()
    {
        var lastKeyTime = 0f;
        // Get max animation time by getting the time of the last key from all curves
        foreach (var curve in _curves)
        {
            if (curve.length != 0) lastKeyTime = Max(lastKeyTime, curve.keys[curve.length - 1].time);
        }

        return lastKeyTime;
    }

    private void ApplyCustomTransformation()
    {
        // When using matrix A, set these to default values
        if (!inputRotationAngle.isFocused) inputRotationAngle.text = "";
        if (!inputReflectionAngle.isFocused) inputReflectionAngle.text = "";
        if (!inputHorizontalShear.isFocused) inputHorizontalShear.text = "";
        if (!inputVerticalShear.isFocused) inputVerticalShear.text = "";

        if (!IsInputValid()) return;

        TransformMesh();
    }


    private bool IsInputValid()
    {
        return inputX0.text != "" && inputX1.text != "" && inputY0.text != "" && inputY1.text != "" &&
               inputX0.text != "-" && inputX1.text != "-" && inputY0.text != "-" && inputY1.text != "-";
    }

    /// <summary>
    /// Transform the cube based on 2D matrix values x0, x1, y0, y1
    /// </summary>
    private void TransformMesh()
    {
        var updatedVertices = new Vector3[_vertices.Length];
        for (var i = 0; i < _vertices.Length; i++)
        {
            updatedVertices[i] = TransformPoint(_vertices[i], x0, x1, y0, y1);
        }

        _mesh.vertices = updatedVertices;
    }

    private static Vector3 TransformPoint(Vector3 point, float x0, float x1, float y0, float y1)
    {
        var newX = point.x * x0 + point.y * y0;
        var newY = point.x * x1 + point.y * y1;
        return new Vector3(newX, newY, point.z);
    }


    private void MoveCamera()
    {
        if (inputX0.isFocused || inputX1.isFocused || inputY0.isFocused || inputY1.isFocused) return;
        if (Input.GetMouseButtonDown(0))
        {
            _prevMousePos = cam.ScreenToViewportPoint(Input.mousePosition);
        }
        else if (Input.GetMouseButton(0))
        {
            var newPosition = cam.ScreenToViewportPoint(Input.mousePosition);
            var direction = _prevMousePos - newPosition;

            cam.transform.position = cube.transform.position;

            cam.transform.Rotate(Vector3.right, direction.y * 180);
            cam.transform.Rotate(Vector3.up, -direction.x * 180,
                Space.World);

            cam.transform.Translate(new Vector3(0, 0, -_camDistToCube));
            _prevMousePos = newPosition;
        }
    }

    /// <summary>
    /// Set the display texts of the 2D matrix
    /// </summary>
    /// <param name="field"></param> the input field (e.g. x0,x1,y0,y1)
    /// <param name="value"></param> value
    /// <param name="usingRotation"></param> whether the animation is using rotation or not (to display "sin" and "cos")
    private void SetText(TMP_InputField field, float value, string usingRotation)
    {
        field.pointSize = 13;
        // If value is close to 0 or 1, set respectively
        value = Abs(value - 0) <= 0.005f ? 0 : value; 
        value = Abs(value - 1) <= 0.005f ? 1 : value;
        
        if (field.name == "angle") angle = value;
        switch (field.name)
        {
            case "x0":
                x0 = value;
                break;
            case "x1":
                x1 = value;
                break;
            case "y0":
                y0 = value;
                break;
            case "y1":
                y1 = value;
                break;
        }

        string textDisplay = $"{value:0.0}";  // value to display (1 decimal)
        if (usingRotation == "none")
        {
            field.text = textDisplay;
        }
        else
        {
            var formattedAngle = $"{angle / 3.14f:0.0}" + "\u03C0"; // radians (1 decimal) + pi symbol
            switch (field.name)
            {
                case "x0":
                    field.text = usingRotation == "both" || usingRotation == "cos"
                        ? "cos(" + formattedAngle + ")"
                        : textDisplay;
                    break;
                case "x1":
                    field.text = usingRotation == "both" || usingRotation == "sin"
                        ? "sin(" + formattedAngle + ")"
                        : textDisplay;
                    break;
                case "y0":
                    field.text = usingRotation == "both" || usingRotation == "sin"
                        ? "-sin(" + formattedAngle + ")"
                        : textDisplay;
                    break;
                case "y1":
                    field.text = usingRotation == "both" || usingRotation == "cos"
                        ? "cos(" + formattedAngle + ")"
                        : textDisplay;
                    break;
            }
        }
    }

    private static void SmoothCurve(AnimationCurve curve)
    {
        for (int i = 0; i < curve.length; i++)
        {
            Keyframe k = curve[i];
            k.inTangent = 0f;
            k.outTangent = 0f;
            curve.MoveKey(i, k);
        }
    }

    private static void SmoothRotationCurve(AnimationCurve curve)
    {
        // Smooth start point
        Keyframe k = curve[curve.length - 21];
        k.inTangent = 0f;
        k.outTangent = 0f;
        curve.MoveKey(curve.length - 21, k);

        // Smooth end point
        k = curve[curve.length - 1];
        k.inTangent = 0f;
        k.outTangent = 0f;
        curve.MoveKey(curve.length - 1, k);
    }

    private static void SetCurveLinear(AnimationCurve curve)
    {
        for (int i = 0; i < curve.keys.Length; ++i)
        {
            float inTangent = 0;
            float outTangent = 0;
            bool inTangentSet = false;
            bool outTangentSet = false;
            Vector2 point1;
            Vector2 point2;
            Vector2 delta;
            Keyframe key = curve[i];

            if (i == 0)
            {
                inTangent = 0;
                inTangentSet = true;
            }

            if (i == curve.keys.Length - 1)
            {
                outTangent = 0;
                outTangentSet = true;
            }

            if (!inTangentSet)
            {
                point1.x = curve.keys[i - 1].time;
                point1.y = curve.keys[i - 1].value;
                point2.x = curve.keys[i].time;
                point2.y = curve.keys[i].value;

                delta = point2 - point1;

                inTangent = delta.y / delta.x;
            }

            if (!outTangentSet)
            {
                point1.x = curve.keys[i].time;
                point1.y = curve.keys[i].value;
                point2.x = curve.keys[i + 1].time;
                point2.y = curve.keys[i + 1].value;

                delta = point2 - point1;

                outTangent = delta.y / delta.x;
            }

            key.inTangent = inTangent;
            key.outTangent = outTangent;
            curve.MoveKey(i, key);
        }
    }

    ///////////////////////////////////////////////////////////////////////////////////////
    /// STRUCTS 
    ///////////////////////////////////////////////////////////////////////////////////////
    readonly struct AnimationDuration
    {
        public float StartTime { get; }
        public float EndTime { get; }

        public AnimationDuration(float start, float end)
        {
            StartTime = start;
            EndTime = end;
        }
    }

    readonly struct RotationInterval
    {
        public float StartTime { get; }
        public float EndTime { get; }
        public string Type { get; } // both sin and cos or not?

        public RotationInterval(float start, float end, string type)
        {
            StartTime = start;
            EndTime = end;
            Type = type;
        }
    }
}