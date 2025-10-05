using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProjectileMover : MonoBehaviour
{
    public float speed = 15f;
    public float hitOffset = 0f;
    public bool UseFirePointRotation;
    public Vector3 rotationOffset = new Vector3(0, 0, 0);
    public GameObject hit;
    public GameObject flash;
    private Rigidbody rb;
    public GameObject[] Detached;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (flash != null)
        {
            var flashInstance = Instantiate(flash, transform.position, Quaternion.identity);
            flashInstance.transform.forward = gameObject.transform.forward;
            var flashPs = flashInstance.GetComponent<ParticleSystem>();
            if (flashPs != null)
            {
                Destroy(flashInstance, flashPs.main.duration);
            }
            else
            {
                var flashPsParts = flashInstance.transform.GetChild(0).GetComponent<ParticleSystem>();
                Destroy(flashInstance, flashPsParts.main.duration);
            }
        }
        Destroy(gameObject, 5);
    }

    void FixedUpdate()
    {
        if (speed != 0)
        {
            rb.velocity = transform.forward * speed;
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        // 检查是否有 Bullet 组件
        Bullet bulletScript = GetComponent<Bullet>();

        // 穿透子弹碰到怪物时，由Bullet脚本处理，这里不销毁
        if (bulletScript != null && bulletScript.IsPiercing() && collision.gameObject.CompareTag("Monster"))
        {
            return;
        }

        // 检查 collision 是否有效
        if (collision == null || collision.contacts.Length == 0)
        {
            return;
        }

        // 检查是否需要反弹
        if (bulletScript != null && bulletScript.ShouldBounce(collision))
        {
            // 如果需要反弹，交给 Bullet 处理，这里什么都不做
            return;
        }

        // 正常的碰撞处理（不反弹的情况）
        //Lock all axes movement and rotation
        rb.constraints = RigidbodyConstraints.FreezeAll;
        speed = 0;

        ContactPoint contact = collision.contacts[0];
        Quaternion rot = Quaternion.FromToRotation(Vector3.up, contact.normal);
        Vector3 pos = contact.point + contact.normal * hitOffset;

        if (hit != null)
        {
            var hitInstance = Instantiate(hit, pos, rot);
            if (UseFirePointRotation)
            {
                hitInstance.transform.rotation = gameObject.transform.rotation * Quaternion.Euler(0, 180f, 0);
            }
            else if (rotationOffset != Vector3.zero)
            {
                hitInstance.transform.rotation = Quaternion.Euler(rotationOffset);
            }
            else
            {
                hitInstance.transform.LookAt(contact.point + contact.normal);
            }

            // 修复：添加完整的空检查
            var hitPs = hitInstance.GetComponent<ParticleSystem>();
            if (hitPs != null)
            {
                Destroy(hitInstance, hitPs.main.duration);
            }
            else if (hitInstance.transform.childCount > 0)
            {
                var hitPsParts = hitInstance.transform.GetChild(0).GetComponent<ParticleSystem>();
                if (hitPsParts != null)
                {
                    Destroy(hitInstance, hitPsParts.main.duration);
                }
                else
                {
                    // 如果子对象也没有粒子系统，默认2秒后销毁
                    Destroy(hitInstance, 2f);
                }
            }
            else
            {
                // 如果没有任何粒子系统，默认2秒后销毁
                Destroy(hitInstance, 2f);
            }
        }

        foreach (var detachedPrefab in Detached)
        {
            if (detachedPrefab != null)
            {
                detachedPrefab.transform.parent = null;
            }
        }
        Destroy(gameObject);
    }
}