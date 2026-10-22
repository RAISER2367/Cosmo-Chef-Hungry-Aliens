using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class TurbulenceZone : MonoBehaviour
{
    [SerializeField] Transform chef;
    [SerializeField] Transform cameraTransform;   // Main Camera
    [SerializeField] Transform areaToCover;       // WorkArea
    [SerializeField] float frequency = 0.7f;      // як швидко міняється напрям знесення
    [SerializeField] float shakeAmount = 0.03f;   // максимальне тремтіння камери, м

    BoxCollider _box;
    CharacterController _cc;
    Vector3 _seed;
    Vector3 _cameraBase;
    bool _inside;

    void Awake()
    {
        _box = GetComponent<BoxCollider>();
        _cc = chef.GetComponent<CharacterController>();
        _seed = Random.insideUnitSphere * 100f;

        if (cameraTransform != null) _cameraBase = cameraTransform.localPosition;
        if (areaToCover != null) FitToArea();
    }

    // Розтягує зону на всі видимі елементи WorkArea
    void FitToArea()
    {
        Renderer[] rs = areaToCover.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return;

        Bounds b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);

        transform.position = b.center;
        transform.rotation = Quaternion.identity;
        transform.localScale = Vector3.one;
        _box.center = Vector3.zero;
        _box.size = b.size + Vector3.up; // запас по висоті
    }

    void Update()
    {
        TurbulenceDirector d = TurbulenceDirector.Instance;
        bool inside = Contains(chef.position);

        if (inside != _inside)
        {
            _inside = inside;
            Debug.Log(inside ? "[Turbulence] вхід у зону" : "[Turbulence] вихід із зони");
        }

        if (d == null || !d.Active || !_inside)
        {
            if (cameraTransform != null) cameraTransform.localPosition = _cameraBase;
            return;
        }

        // Напрям знесення плавно змінюється, тому це не схоже на рівномірний вітер
        float t = Time.time * frequency;
        Vector3 dir = new Vector3(
            Mathf.PerlinNoise(_seed.x, t) * 2f - 1f,
            0f,
            Mathf.PerlinNoise(_seed.z, t) * 2f - 1f);

        // CharacterController сам зупиняє знос об стіни й ящики
        _cc.Move(dir.normalized * d.CurrentStrength * Time.deltaTime);

        if (cameraTransform != null)
            cameraTransform.localPosition = _cameraBase + Random.insideUnitSphere * shakeAmount * d.Shake;
    }

    bool Contains(Vector3 world)
    {
        Vector3 local = transform.InverseTransformPoint(world) - _box.center;
        Vector3 half = _box.size * 0.5f;

        return Mathf.Abs(local.x) < half.x
            && Mathf.Abs(local.y) < half.y + 1f
            && Mathf.Abs(local.z) < half.z;
    }
}