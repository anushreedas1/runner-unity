using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerRunner : MonoBehaviour
{
    [Header("Character model (drag the model's object here)")]
    [SerializeField] private CharacterAnimator character;
    [SerializeField] private float characterYOffset = 0f;
    [SerializeField] private float characterYaw = 0f;

    [Header("Movement")]
    [SerializeField] private float laneDistance = 2f;
    [SerializeField] private float laneChangeSpeed = 12f;

    [Header("Jump and Slide")]
    [SerializeField] private float jumpForce = 10f;
    [SerializeField] private float gravity = 30f;
    [SerializeField] private float slideDuration = 0.8f;
    [SerializeField] private float slideColliderHeight = 1f;
    [SerializeField] private float fastFallSpeed = 15f;

    [Header("Swipe")]
    [SerializeField] private float swipeThreshold = 50f;

    private const float RunReferenceSpeed = 9f;

    private int currentLane = 1; // 0 = left, 1 = middle, 2 = right
    private float verticalVelocity;
    private bool isSliding;
    private float slideTimer;
    private float standGroundY;

    // Sliding makes the collision shape shorter
    private CapsuleCollider capsule;
    private float standHeight;
    private Vector3 standCenter;

    private Vector2 swipeStart;
    private bool swipeHandled = true;

    // Looks: the stand-in capsule, or the character model
    private Renderer playerRenderer;
    private Renderer[] modelRenderers;
    private Material[] modelMaterials;
    private string[] modelColorProperties;
    private Color[] modelOriginalColors;
    private Color appliedColor;
    private bool hasAppliedColor;

    // Animation bookkeeping
    private bool idleStarted;
    private bool runStarted;
    private bool deathStarted;
    private bool wasAirborne;
    private bool wasStumbled;

    private readonly Color shieldColor = new Color(0.3f, 0.9f, 1f);

    void Start()
    {
        standGroundY = transform.position.y;

        capsule = GetComponent<CapsuleCollider>();
        if (capsule != null)
        {
            standHeight = capsule.height;
            standCenter = capsule.center;
        }

        playerRenderer = GetComponent<Renderer>();

        if (character != null) SetUpCharacter();

        // A kinematic Rigidbody lets Unity report trigger hits with obstacles, coins and power-ups
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    private void SetUpCharacter()
    {
        // The capsule stays as the invisible collision shape
        if (playerRenderer != null) playerRenderer.enabled = false;

        // Feet on the ground, facing forward
        character.transform.localPosition = new Vector3(0f, -standGroundY + characterYOffset, 0f);
        character.transform.localRotation = Quaternion.Euler(0f, characterYaw, 0f);

        // Remember the model's materials, so it can be tinted by the shop colour and the shield
        modelRenderers = character.GetComponentsInChildren<Renderer>(true);

        List<Material> materials = new List<Material>();
        foreach (Renderer modelRenderer in modelRenderers)
        {
            foreach (Material material in modelRenderer.materials)
            {
                materials.Add(material);
            }
        }

        modelMaterials = materials.ToArray();
        modelColorProperties = new string[modelMaterials.Length];
        modelOriginalColors = new Color[modelMaterials.Length];

        for (int i = 0; i < modelMaterials.Length; i++)
        {
            Material material = modelMaterials[i];

            if (material.HasProperty("_BaseColor")) modelColorProperties[i] = "_BaseColor";
            else if (material.HasProperty("_Color")) modelColorProperties[i] = "_Color";
            else modelColorProperties[i] = null;

            modelOriginalColors[i] = modelColorProperties[i] != null
                ? material.GetColor(modelColorProperties[i])
                : Color.white;
        }
    }

    void Update()
    {
        GameManager gm = GameManager.Instance;

        // Wait in the menu and shop, and stop after game over
        if (gm != null && !gm.IsPlaying)
        {
            SetVisible(true);
            ApplyColor(gm.SkinColor);
            AnimateWhileWaiting(gm);
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
        verticalVelocity -= gravity * Time.deltaTime;
        pos.y += verticalVelocity * Time.deltaTime;

        if (pos.y <= standGroundY)
        {
            pos.y = standGroundY;
            verticalVelocity = 0f;
        }

        transform.position = pos;

        AnimateWhileRunning(gm, speed);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<Obstacle>() == null) return;

        if (GameManager.Instance != null) GameManager.Instance.PlayerHitObstacle();
    }

    // ---------- Looks ----------

    private void SetVisible(bool visible)
    {
        if (character != null)
        {
            foreach (Renderer modelRenderer in modelRenderers)
            {
                modelRenderer.enabled = visible;
            }
        }
        else if (playerRenderer != null)
        {
            playerRenderer.enabled = visible;
        }
    }

    private void ApplyColor(Color color)
    {
        if (hasAppliedColor && color == appliedColor) return;

        appliedColor = color;
        hasAppliedColor = true;

        if (character != null)
        {
            for (int i = 0; i < modelMaterials.Length; i++)
            {
                if (modelColorProperties[i] == null) continue;
                modelMaterials[i].SetColor(modelColorProperties[i], modelOriginalColors[i] * color);
            }
        }
        else if (playerRenderer != null)
        {
            playerRenderer.material.color = color;
        }
    }

    // ---------- Animation ----------

    private void AnimateWhileWaiting(GameManager gm)
    {
        if (character == null) return;

        if (gm.IsGameOver)
        {
            if (!deathStarted)
            {
                character.Play(CharacterAnimator.State.Death);
                deathStarted = true;
            }
        }
        else if (!idleStarted)
        {
            character.Play(CharacterAnimator.State.Idle);
            idleStarted = true;
        }
    }

    private void AnimateWhileRunning(GameManager gm, float speed)
    {
        if (character == null) return;

        bool grounded = IsGrounded();

        if (!runStarted)
        {
            character.Play(CharacterAnimator.State.Run);
            runStarted = true;
        }

        character.SetRunSpeed(Mathf.Clamp(speed / RunReferenceSpeed, 0.8f, 1.8f));

        bool stumbled = gm != null && gm.IsStumbled;
        if (stumbled && !wasStumbled && grounded && !isSliding)
        {
            character.Play(CharacterAnimator.State.Stumble);
        }
        wasStumbled = stumbled;

        // Back to running when we land
        if (wasAirborne && grounded && !isSliding)
        {
            character.Play(CharacterAnimator.State.Run);
        }
        wasAirborne = !grounded;
    }

    // ---------- Input ----------

    private bool IsGrounded()
    {
        return transform.position.y <= standGroundY + 0.01f;
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

        // Stretch the jump animation to last as long as the jump does
        if (character != null) character.Play(CharacterAnimator.State.Jump, 2f * jumpForce / gravity);
    }

    private void Slide()
    {
        if (isSliding) return;

        bool wasGrounded = IsGrounded();

        SetSliding(true);
        slideTimer = slideDuration;
        SoundManager.Play(SoundManager.Sfx.Slide);

        if (character != null) character.Play(CharacterAnimator.State.Slide, slideDuration);

        // Sliding in mid-air makes you drop fast
        if (!wasGrounded) verticalVelocity = -fastFallSpeed;
    }

    private void UpdateSlideTimer()
    {
        if (!isSliding) return;

        slideTimer -= Time.deltaTime;
        if (slideTimer <= 0f) EndSlide();
    }

    private void EndSlide()
    {
        SetSliding(false);

        if (character != null && IsGrounded()) character.Play(CharacterAnimator.State.Run);
    }

    // Sliding keeps the feet on the ground and makes the collision shape shorter,
    // so the player fits under the fire
    private void SetSliding(bool sliding)
    {
        isSliding = sliding;

        if (capsule == null) return;

        if (sliding)
        {
            capsule.height = slideColliderHeight;
            capsule.center = new Vector3(standCenter.x, standCenter.y - (standHeight - slideColliderHeight) / 2f, standCenter.z);
        }
        else
        {
            capsule.height = standHeight;
            capsule.center = standCenter;
        }
    }
}