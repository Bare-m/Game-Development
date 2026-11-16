using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UnityEngine.Events;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 4f;

    [Tooltip("How quickly movement speeds up and slows down.")]
    [SerializeField, Min(0.1f)]
    private float acceleration = 20f;

    [SerializeField] private float jumpHeight = 1.5f;
    [SerializeField] private float gravity = -40f;

    [Header("Mouse Look")]
    [SerializeField] private float mouseSensitivity = 2f;

    [SerializeField, Range(1f, 89f)]
    private float maxLookAngle = 80f;

    [Header("Camera")]
    [Tooltip("Main Camera must be directly parented to the controlled character.")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField, Range(10f, 90f)] private float minZoomFieldOfView = 30f;
    [SerializeField, Range(30f, 120f)] private float maxZoomFieldOfView = 80f;
    [SerializeField, Min(1f)] private float zoomSensitivity = 40f;

    [Header("Visible Head - Optional")]
    [Tooltip("Assign a real head bone, NOT Player or body-mesh.")]
    [SerializeField] private Transform headBone;

    [Header("Pushing")]
    [SerializeField] private float pushPower = 2f;

    [Header("Failure")]
    [SerializeField] private float fallResetHeight = -10f;
    [SerializeField, Min(0f)] private float autoRespawnDelay = 1.5f;

    private CharacterController controller;
    private Camera playerCamera;
    private Transform currentPlatform;

    private Vector3 horizontalVelocity;
    private Vector3 respawnPosition;
    private Quaternion respawnRotation;
    private float verticalVelocity;
    private float cameraPitch;

    private Quaternion headRestLocalRotation;
    private GameObject startScreen;
    private GameObject failureScreen;

    private bool gameStarted;
    private bool hasFailed;
    private bool isQuitting;

    private void Start()
    {
        controller = GetComponent<CharacterController>();
        respawnPosition = transform.position;
        respawnRotation = Quaternion.Euler(
            0f,
            transform.eulerAngles.y,
            0f
        );

        // Prefer a Camera inside Player, then fall back to the scene camera.
        if (cameraTransform == null)
        {
            Camera childCamera = GetComponentInChildren<Camera>(true);

            if (childCamera != null)
            {
                cameraTransform = childCamera.transform;
            }
        }

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        if (cameraTransform == null ||
            cameraTransform.GetComponent<Camera>() == null)
        {
            Debug.LogError(
                "PlayerController: Assign a Camera to Camera Transform " +
                "or tag the scene camera MainCamera.",
                this
            );

            enabled = false;
            return;
        }

        playerCamera = cameraTransform.GetComponent<Camera>();
        playerCamera.fieldOfView = Mathf.Clamp(
            playerCamera.fieldOfView,
            minZoomFieldOfView,
            maxZoomFieldOfView
        );

        if (cameraTransform.parent != transform)
        {
            cameraTransform.SetParent(transform, false);
            cameraTransform.localPosition =
                Vector3.up * (controller.height * 0.9f);
        }

        cameraPitch = 0f;
        cameraTransform.localRotation = Quaternion.identity;
        cameraTransform.localScale = Vector3.one;

        // The optional head must belong to the player's skeleton.
        if (headBone != null)
        {
            if (headBone == transform ||
                headBone == cameraTransform ||
                !headBone.IsChildOf(transform))
            {
                Debug.LogWarning(
                    "Head Bone must be a head bone inside Player. " +
                    "The optional head movement has been disabled.",
                    this
                );

                headBone = null;
            }
            else
            {
                headRestLocalRotation = headBone.localRotation;
            }
        }

        Time.timeScale = 0f;
        SetCursorLocked(false);
        ShowStartScreen();
    }

    private void Update()
    {
        if (isQuitting || hasFailed || !gameStarted)
        {
            return;
        }

        if (transform.position.y < fallResetHeight)
        {
            ShowFailureScreen();
            return;
        }

        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;

        // Escape releases the cursor.
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            SetCursorLocked(false);
        }
        // Click the Game view to capture the cursor again.
        else if (Cursor.lockState != CursorLockMode.Locked &&
                 mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            SetCursorLocked(true);
        }

        bool canControl =
            Cursor.lockState == CursorLockMode.Locked;

        if (canControl)
        {
            HandleMouseLook();
            HandleCameraZoom();
        }

        HandleMovement(canControl);
        UpdateMovingPlatformParent();
    }

    private void HandleMouseLook()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            return;
        }

        Vector2 mouseDelta = mouse.delta.ReadValue();
        float mouseX = mouseDelta.x * mouseSensitivity * 0.1f;
        float mouseY = mouseDelta.y * mouseSensitivity * 0.1f;

        // Horizontal mouse movement turns Player and its visible body.
        transform.Rotate(0f, mouseX, 0f, Space.World);

        // Vertical mouse movement tilts the Camera, not the whole body.
        cameraPitch -= mouseY;
        cameraPitch = Mathf.Clamp(
            cameraPitch,
            -maxLookAngle,
            maxLookAngle
        );

        cameraTransform.localRotation =
            Quaternion.Euler(cameraPitch, 0f, 0f);
    }

    private void HandleCameraZoom()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            return;
        }

        float scroll = mouse.scroll.ReadValue().y / 1200f;
        if (Mathf.Approximately(scroll, 0f))
        {
            return;
        }

        playerCamera.fieldOfView = Mathf.Clamp(
            playerCamera.fieldOfView - scroll * zoomSensitivity,
            minZoomFieldOfView,
            maxZoomFieldOfView
        );
    }

    private void HandleMovement(bool canControl)
    {
        Vector2 input = Vector2.zero;
        Keyboard keyboard = Keyboard.current;

        if (canControl && keyboard != null)
        {
            input = new Vector2(
                (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f) -
                (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f),
                (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1f : 0f) -
                (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1f : 0f)
            );
        }

        // Prevent faster diagonal movement.
        input = Vector2.ClampMagnitude(input, 1f);

        // Move along the ground in the direction Player is facing.
        // Looking up does not make the player fly upwards.
        Vector3 forward = Vector3.ProjectOnPlane(
            transform.forward,
            Vector3.up
        ).normalized;

        Vector3 right = Vector3.Cross(Vector3.up, forward);

        Vector3 targetVelocity =
            (forward * input.y + right * input.x) * moveSpeed;

        // Smooth acceleration and stopping.
        if (canControl)
        {
            horizontalVelocity = Vector3.MoveTowards(
                horizontalVelocity,
                targetVelocity,
                acceleration * Time.deltaTime
            );
        }
        else
        {
            horizontalVelocity = Vector3.zero;
        }

        if (controller.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }

        if (canControl &&
            controller.isGrounded &&
            keyboard != null &&
            keyboard.spaceKey.wasPressedThisFrame)
        {
            verticalVelocity =
                Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        verticalVelocity += gravity * Time.deltaTime;

        Vector3 movement =
            horizontalVelocity + Vector3.up * verticalVelocity;

        controller.Move(movement * Time.deltaTime);
    }

    private void LateUpdate()
    {
        if (headBone == null || hasFailed || isQuitting)
        {
            return;
        }

        // Player rotation already turns the head left/right.
        // Add the up/down look to the visible head.
        //
        // Using Player's right axis avoids assuming that the
        // imported head bone's own X-axis points sideways.
        Quaternion restWorldRotation =
            headBone.parent.rotation * headRestLocalRotation;

        headBone.rotation =
            Quaternion.AngleAxis(cameraPitch, transform.right) *
            restWorldRotation;
    }

    private void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked
            ? CursorLockMode.Locked
            : CursorLockMode.None;

        Cursor.visible = !locked;
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (hit.collider.GetComponentInParent<returnBarrel>() != null)
        {
            ShowFailureScreen();
            return;
        }

        Rigidbody body = hit.collider.attachedRigidbody;

        if (body == null || body.isKinematic)
        {
            return;
        }

        // Do not push objects underneath the Player.
        if (hit.moveDirection.y < -0.3f)
        {
            return;
        }

        Vector3 pushDirection = new Vector3(
            hit.moveDirection.x,
            0f,
            hit.moveDirection.z
        );

        float pushSpeed = moveSpeed * (pushPower * 0.5f);

        // Preserve vertical velocity so pushed objects still fall.
        Vector3 bodyVelocity = body.linearVelocity;
        bodyVelocity.x = pushDirection.x * pushSpeed;
        bodyVelocity.z = pushDirection.z * pushSpeed;

        body.linearVelocity = bodyVelocity;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Orb"))
        {
            Debug.Log("Collected orb: " + other.gameObject.name);
            Destroy(other.gameObject);
        }
    }

    public void SetCheckpoint(Vector3 position, Quaternion rotation)
    {
        respawnPosition = position;
        respawnRotation = Quaternion.Euler(
            0f,
            rotation.eulerAngles.y,
            0f
        );

        Debug.Log("Checkpoint saved at " + respawnPosition, this);
    }

    private void UpdateMovingPlatformParent()
    {
        Transform platform = null;

        if (controller.isGrounded &&
            Physics.Raycast(
                controller.bounds.center,
                Vector3.down,
                out RaycastHit hit,
                controller.bounds.extents.y + 0.2f,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore
            ))
        {
            MovingPlatformLR platformLR =
                hit.collider.GetComponentInParent<MovingPlatformLR>();

            if (platformLR != null)
            {
                platform = platformLR.transform;
            }
            else
            {
                MovingPlatformRL platformRL =
                    hit.collider.GetComponentInParent<MovingPlatformRL>();

                if (platformRL != null)
                {
                    platform = platformRL.transform;
                }
            }
        }

        if (platform == currentPlatform)
        {
            return;
        }

        currentPlatform = platform;

        transform.SetParent(
            platform,
            true
        );
    }

    private void ShowFailureScreen()
    {
        if (hasFailed)
        {
            return;
        }

        hasFailed = true;
        controller.enabled = false;
        SetCursorLocked(false);

        // Canvas.
        failureScreen = CreateOverlayCanvas("Failure Screen", 100);

        // Background.
        var panelObject = new GameObject(
            "Failure Panel",
            typeof(RectTransform),
            typeof(Image)
        );

        panelObject.transform.SetParent(failureScreen.transform, false);

        var panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        panelObject.GetComponent<Image>().color =
            new Color(0f, 0f, 0f, 0.7f);

        // Title.
        CreateLabel(
            panelObject.transform,
            "Failure Message",
            "RESPAWNING...",
            64,
            new Vector2(0f, 100f),
            new Vector2(800f, 120f)
        );

        // Restart button.
        CreateActionButton(
            panelObject.transform,
            "Respawn Button",
            "RESPAWN NOW",
            new Vector2(0f, -40f),
            RespawnPlayer
        );

        EnsureEventSystem();
        Invoke(nameof(RespawnPlayer), autoRespawnDelay);
    }

    private void ShowStartScreen()
    {
        startScreen = CreateOverlayCanvas("Start Screen", 100);

        var panelObject = new GameObject(
            "Start Panel",
            typeof(RectTransform),
            typeof(Image)
        );
        panelObject.transform.SetParent(startScreen.transform, false);

        var panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        panelObject.GetComponent<Image>().color =
            new Color(0.035f, 0.07f, 0.065f, 0.94f);

        CreateLabel(
            panelObject.transform,
            "Game Title",
            "PLATFORMER",
            72,
            new Vector2(0f, 115f),
            new Vector2(1000f, 130f)
        );

        CreateLabel(
            panelObject.transform,
            "Subtitle",
            "READY FOR A NEW RUN?",
            24,
            new Vector2(0f, 35f),
            new Vector2(800f, 55f)
        );

        CreateActionButton(
            panelObject.transform,
            "Start Button",
            "START",
            new Vector2(0f, -70f),
            StartGame
        );

        EnsureEventSystem();
    }

    private void StartGame()
    {
        gameStarted = true;
        Time.timeScale = 1f;
        SetCursorLocked(true);

        if (startScreen != null)
        {
            Destroy(startScreen);
            startScreen = null;
        }
    }

    private GameObject CreateOverlayCanvas(string objectName, int sortingOrder)
    {
        var canvasObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster)
        );

        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        return canvasObject;
    }

    private void CreateActionButton(
        Transform parent,
        string objectName,
        string label,
        Vector2 position,
        UnityAction onClick)
    {
        var buttonObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(Image),
            typeof(Button)
        );
        buttonObject.transform.SetParent(parent, false);

        var buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = position;
        buttonRect.sizeDelta = new Vector2(320f, 90f);

        var buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = new Color(0.2f, 0.75f, 0.45f);

        var button = buttonObject.GetComponent<Button>();
        button.targetGraphic = buttonImage;
        button.onClick.AddListener(onClick);

        CreateLabel(
            buttonObject.transform,
            "Label",
            label,
            36,
            Vector2.zero,
            new Vector2(320f, 90f)
        );

    }

    private void EnsureEventSystem()
    {
        EventSystem eventSystem = EventSystem.current;

        if (eventSystem == null)
        {
            var eventSystemObject = new GameObject(
                "EventSystem",
                typeof(EventSystem)
            );
            eventSystem = eventSystemObject.GetComponent<EventSystem>();
        }

        InputSystemUIInputModule inputModule =
            eventSystem.GetComponent<InputSystemUIInputModule>();

        if (inputModule == null)
        {
            inputModule =
                eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            inputModule.AssignDefaultActions();
        }

        foreach (BaseInputModule module in
                 eventSystem.GetComponents<BaseInputModule>())
        {
            if (module != inputModule)
            {
                module.enabled = false;
            }
        }

        if (inputModule.point == null || inputModule.leftClick == null)
        {
            inputModule.AssignDefaultActions();
        }

        inputModule.enabled = true;
    }

    private void CreateLabel(
        Transform parent,
        string objectName,
        string content,
        int fontSize,
        Vector2 position,
        Vector2 size)
    {
        var labelObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(Text)
        );

        labelObject.transform.SetParent(parent, false);

        var rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        var label = labelObject.GetComponent<Text>();
        label.font =
            Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.text = content;
        label.fontSize = fontSize;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.raycastTarget = false;
    }

    private void RespawnPlayer()
    {
        CancelInvoke(nameof(RespawnPlayer));
        Time.timeScale = 1f;
        transform.SetParent(null, true);
        currentPlatform = null;
        horizontalVelocity = Vector3.zero;
        verticalVelocity = 0f;
        transform.SetPositionAndRotation(respawnPosition, respawnRotation);
        cameraPitch = 0f;

        if (cameraTransform != null)
        {
            cameraTransform.localRotation = Quaternion.identity;
        }

        if (headBone != null)
        {
            headBone.localRotation = headRestLocalRotation;
        }

        controller.enabled = true;
        hasFailed = false;
        SetCursorLocked(true);

        if (failureScreen != null)
        {
            Destroy(failureScreen);
            failureScreen = null;
        }
    }

    private void OnDisable()
    {
        SetCursorLocked(false);
    }

    private void OnApplicationQuit()
    {
        isQuitting = true;
        Time.timeScale = 1f;
    }
}
