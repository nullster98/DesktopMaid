using UnityEngine;

public class DragController : MonoBehaviour
{
    #region Inspector Fields
    [Header("컨트롤 설정")]
    [SerializeField] private float moveSpeed = 10f;
    [SerializeField] private float rotationSpeed = 5f;

    [Header("개별 스케일 설정 (LocalScale)")]
    [SerializeField] private float individualScaleSpeed = 0.05f;
    [SerializeField] private float minIndividualScale = 0.1f;
    [SerializeField] private float maxIndividualScale = 1.5f;
    #endregion

    #region Private Fields
    private Camera mainCamera;
    private Transform rootTransform;
    private bool isDragging = false;
    private Vector3 dragOffset;
    private float fixedZ;
    private bool isRotating = false;
    private Animator rootAnimator;
    private VRMAutoActivate autoActivate;
    private SnapAwareVRM snapAware;
    #endregion

    #region Unity Lifecycle
    void Start()
    {
        mainCamera = Camera.main;
        rootTransform = transform.root;
        rootAnimator = rootTransform.GetComponent<Animator>();
        autoActivate = rootTransform.GetComponent<VRMAutoActivate>();
        snapAware = rootTransform.GetComponent<SnapAwareVRM>();
    }

    void Update()
    {
        HandleMouseInput();
    }
    #endregion

    #region Core Logic
    private void HandleMouseInput()
    {
        // 좌클릭으로 드래그 시작
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit) && hit.transform.root == rootTransform)
            {
                isDragging = true;
                fixedZ = rootTransform.position.z;
                dragOffset = rootTransform.position - hit.point;

                if (rootAnimator != null) rootAnimator.SetBool("Drag", true);
                rootTransform.SetAsLastSibling();

                if (snapAware != null)
                {
                    snapAware.StopSnappingOnDrag();
                }
            }
        }
        // 좌클릭 드래그 종료
        else if (Input.GetMouseButtonUp(0))
        {
            if (isDragging)
            {
                isDragging = false;
                if (rootAnimator != null) rootAnimator.SetBool("Drag", false);

                if (snapAware != null)
                {
                    snapAware.OnDragEnd(rootTransform.position.z);
                }
            }
        }

        // 드래그 중: 위치 이동 및 스크롤 휠에 의한 스케일 조절
        if (isDragging)
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            Plane plane = new Plane(Vector3.forward, new Vector3(0, 0, fixedZ));

            if (plane.Raycast(ray, out float distance))
            {
                Vector3 point = ray.GetPoint(distance);
                Vector3 targetPosition = point + dragOffset;
                targetPosition.z = fixedZ;
                rootTransform.position = Vector3.Lerp(rootTransform.position, targetPosition, Time.deltaTime * moveSpeed);
            }
            HandleIndividualScalingWithScrollWheel();
        }

        // 우클릭으로 회전 시작
        if (Input.GetMouseButtonDown(1))
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit) && hit.transform.root == rootTransform)
            {
                isRotating = true;
                if (autoActivate != null) autoActivate.SetUserRotationStatus(true);
            }
        }

        // 우클릭 회전 종료
        if (Input.GetMouseButtonUp(1))
        {
            if (isRotating)
            {
                isRotating = false;
                if (autoActivate != null)
                {
                    autoActivate.SetUserRotationStatus(false);
                    autoActivate.SyncTargetRotationToCurrent();
                }
            }
        }

        // 회전 중: 마우스 X 이동으로 Y축 회전
        if (isRotating)
        {
            float rotateInput = Input.GetAxis("Mouse X") * rotationSpeed;
            rootTransform.Rotate(Vector3.up, -rotateInput, Space.World);
            // 수평 회전만 적용 (Z회전 고정)
            var euler = rootTransform.rotation.eulerAngles;
            euler.z = 0;
            rootTransform.rotation = Quaternion.Euler(euler);
        }
    }

    // 마우스 휠로 개별 캐릭터 스케일 조절
    private void HandleIndividualScalingWithScrollWheel()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) < 0.01f) return;

        // 드래그 중 휠 스크롤: 캐릭터의 로컬 스케일 조절
        Vector3 currentScale = rootTransform.localScale;
        float newScaleValue = currentScale.x + scroll * individualScaleSpeed;
        newScaleValue = Mathf.Clamp(newScaleValue, minIndividualScale, maxIndividualScale);

        rootTransform.localScale = Vector3.one * newScaleValue;
    }
    #endregion
}
