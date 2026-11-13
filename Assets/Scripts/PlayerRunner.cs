using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerRunner : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float laneDistance = 2f;
    [SerializeField] private float laneChangeSpeed = 12f;

    [Header("Jump and Slide")]
    [SerializeField] private float jumpForce = 10f;
    [SerializeField] private float gravity = 30f;
    [SerializeField] private float slideDuration = 0.8f;
    [SerializeField] private float slideScaleY = 0.5f;
    [SerializeField] private float fastFallSpeed = 15f;

    [Header("Swipe")]
    [SerializeField] private float swipeThreshold = 50f;

    private int currentLane = 1; // 0 = left, 1 = middle, 2 = right
    private float verticalVelocity;
    private bool isSliding;
    private float slideTimer;

    private Vector3 standingScale;
    private float standGroundY;
    private float slideGroundY;

    private Vector2 swipeStart;
    private bool swipeHandled = true;

    private Renderer playerRenderer;
    private Color appliedColor;
    private bool hasAppliedColor;

    private readonly Color shieldColor = new Color(0.3f, 0.9f, 1f);

    void Start()
    {
        standingScale = transform.localScale;
        standGroundY = transform.position.y;
        slideGroundY = standGroundY * slideScaleY;

        playerRenderer = GetComponent<Renderer>();

        // A kinematic Rigidbody lets Unity report trigger hits with obstacles, coins and power-ups
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    void Update()
    {
        GameManager gm = GameManager.Instance;

        // Wait in the menu and shop, and stop after game over
        if (gm != null && !gm.IsPlaying)
        {
            SetVisible(true);
            ApplyColor(gm.SkinColor);
            return;
        }

        // Blink while protected after a stumble or a broken shield
        bool blinkOff = gm != null && gm.IsInvulnerable && Mathf.FloorToInt(Time.time * 12f) % 2 == 1;
        SetVisible(!blinkOff);

        if (gm != null) ApplyColor(gm.ShieldActive ? shieldColor : gm.SkinColor);

        ReadInput();
        UpdateSlideTimer();

        float speed = gm != null ? gm.CurrentSpeed : 8f;

        Vector3 pos = transform.position;

        // Run forward
        pos.z += speed * Time.deltaTime;

        // Glide toward the target lane
        float targetX = (currentLane - 1) * laneDistance;
        pos.x = Mathf.MoveTowards(pos.x, targetX, laneChangeSpeed * Time.deltaTime);

        // Gravity and ground
        float groundY = isSliding ? slideGroundY : standGroundY;
        verticalVelocity -= gravity * Time.deltaTime;
        pos.y += verticalVelocity * Time.deltaTime;

        if (pos.y <= groundY)
        {
            pos.y = groundY;
            verticalVelocity = 0f;
        }

        transform.position = pos;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<Obstacle>() == null) return;

        if (GameManager.Instance != null) GameManager.Instance.PlayerHitObstacle();
    }

    private void SetVisible(bool visible)
    {
        if (playerRenderer != null) playerRenderer.enabled = visible;
    }

    private void ApplyColor(Color color)
    {
        if (playerRenderer == null) return;
        if (hasAppliedColor && color == appliedColor) return;

        playerRenderer.material.color = color;
        appliedColor = color;
        hasAppliedColor = true;
    }

    private bool IsGrounded()
    {
        float groundY = isSliding ? slideGroundY : standGroundY;
        return transform.position.y <= groundY + 0.01f;
    }

    private void ReadInput()
    {
        // Keyboard (for testing on PC)
        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) ChangeLane(-1);
            if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) ChangeLane(1);
            if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) Jump();
            if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) Slide();
        }

        // Swipe (mouse drag on PC, finger on phone)
        Pointer pointer = Pointer.current;
        if (pointer != null)
        {
            if (pointer.press.wasPressedThisFrame)
            {
                swipeStart = pointer.position.ReadValue();
                swipeHandled = false;
            }
            else if (pointer.press.isPressed && !swipeHandled)
            {
                Vector2 delta = pointer.position.ReadValue() - swipeStart;

                if (delta.magnitude >= swipeThreshold)
                {
                    swipeHandled = true;

                    if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
                    {
                        ChangeLane(delta.x > 0 ? 1 : -1);
                    }
                    else if (delta.y > 0)
                    {
                        Jump();
                    }
                    else
                    {
                        Slide();
                    }
                }
            }
        }
    }

    private void ChangeLane(int direction)
    {
        int newLane = Mathf.Clamp(currentLane + direction, 0, 2);
        if (newLane == currentLane) return;

        currentLane = newLane;
        SoundManager.Play(SoundManager.Sfx.Lane);
    }

    private void Jump()
    {
        if (!IsGrounded()) return;

        if (isSliding) EndSlide();
        verticalVelocity = jumpForce;
        SoundManager.Play(SoundManager.Sfx.Jump);
    }

    private void Slide()
    {
        if (isSliding) return;

        bool wasGrounded = IsGrounded();

        isSliding = true;
        slideTimer = slideDuration;
        transform.localScale = new Vector3(standingScale.x, standingScale.y * slideScaleY, standingScale.z);
        SoundManager.Play(SoundManager.Sfx.Slide);

        if (wasGrounded)
        {
            // Keep the capsule sitting on the ground
            Vector3 p = transform.position;
            p.y = slideGroundY;
            transform.position = p;
        }
        else
        {
            // Sliding in mid-air makes you drop fast
            verticalVelocity = -fastFallSpeed;
        }
    }

    private void UpdateSlideTimer()
    {
        if (!isSliding) return;

        slideTimer -= Time.deltaTime;
        if (slideTimer <= 0f) EndSlide();
    }

    private void EndSlide()
    {
        isSliding = false;
        transform.localScale = standingScale;
    }
}