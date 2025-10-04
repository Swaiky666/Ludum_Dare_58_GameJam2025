using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float dashDistance = 3f;
    public float dashCooldown = 2f;

    public float health = 100f;
    public float currentShield = 50f;
    public float maxShield = 100f;
    public float shieldRegenSpeed = 10f;

    private float dashCooldownTimer = 0f;
    private CharacterController controller;

    void Start()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        Vector3 movement = new Vector3(horizontal, 0f, vertical).normalized * moveSpeed * Time.deltaTime;
        controller.Move(movement);

        if (Input.GetKeyDown(KeyCode.Space) && dashCooldownTimer <= 0f)
        {
            Vector3 dashDirection = new Vector3(horizontal, 0f, vertical).normalized;
            if (dashDirection != Vector3.zero)
            {
                RaycastHit hit;
                if (Physics.Raycast(transform.position, dashDirection, out hit, dashDistance))
                {
                    float safeDistance = hit.distance - controller.radius;
                    if (safeDistance > 0)
                    {
                        controller.Move(dashDirection * safeDistance);
                    }
                }
                else
                {
                    controller.Move(dashDirection * dashDistance);
                }
                dashCooldownTimer = dashCooldown;
            }
        }

        if (dashCooldownTimer > 0f)
        {
            dashCooldownTimer -= Time.deltaTime;
        }

        if (currentShield < maxShield)
        {
            currentShield += shieldRegenSpeed * Time.deltaTime;
            if (currentShield > maxShield)
            {
                currentShield = maxShield;
            }
        }
    }
}