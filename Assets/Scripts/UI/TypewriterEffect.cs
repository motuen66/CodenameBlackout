using UnityEngine;
using TMPro;
using System.Collections;

public class TypewriterEffect : MonoBehaviour
{
    // --- SINGLETON IMPLEMENTATION ---
    // Biến static để lưu trữ thể hiện duy nhất của script này.
    private static TypewriterEffect _instance;

    // Thuộc tính công khai static để truy cập thể hiện duy nhất.
    public static TypewriterEffect Instance
    {
        get
        {
            // Nếu chưa có thể hiện nào, cố gắng tìm trong cảnh.
            if (_instance == null)
            {
                _instance = FindObjectOfType<TypewriterEffect>();

                // Nếu vẫn không tìm thấy, log lỗi.
                if (_instance == null)
                {
                    Debug.LogError("TypewriterEffect: Không tìm thấy thể hiện nào trong cảnh! Đảm bảo TypewriterEffect được đặt trên một GameObject.");
                }
            }
            return _instance;
        }
    }
    // --- KẾT THÚC SINGLETON IMPLEMENTATION ---


    [Header("Cài đặt Văn bản")]
    public TextMeshProUGUI textMeshPro;
    [Range(1f, 100f)]
    public float typingSpeed = 50f;

    [Header("Cài đặt Âm thanh")]
    public AudioClip typingSound;
    [Range(0f, 1f)]
    public float typingSoundVolume = 0.5f;

    private Coroutine typingCoroutine;
    private string fullText;
    private AudioSource audioSource;
    // Đã bỏ biến 'instance' cũ vì nó không được dùng đúng cách cho Singleton.


    void Awake()
    {
        // Logic kiểm tra singleton để đảm bảo chỉ có một thể hiện.
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject); // Hủy thể hiện trùng lặp này
            Debug.LogWarning("TypewriterEffect: Đã tìm thấy thể hiện trùng lặp, đang hủy.", this);
            return;
        }
        _instance = this; // Gán thể hiện này làm thể hiện duy nhất

        // Khởi tạo TextMeshProUGUI nếu chưa được gán trong Inspector
        if (textMeshPro == null)
        {
            textMeshPro = GetComponent<TextMeshProUGUI>();
            if (textMeshPro == null)
            {
                Debug.LogError("TypewriterEffect: Không tìm thấy TextMeshProUGUI component!", this);
                enabled = false;
                return;
            }
        }

        // Khởi tạo AudioSource nếu chưa có
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0;
        }
    }

    // Bắt đầu hiệu ứng đánh máy cho một đoạn văn bản mới.
    public void StartTyping(string textToDisplay)
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
        }

        fullText = textToDisplay;
        textMeshPro.text = "";
        typingCoroutine = StartCoroutine(TypewriterCoroutine());
    }

    // Coroutine xử lý việc hiển thị từng ký tự một.
    IEnumerator TypewriterCoroutine()
    {
        float delay = 1f / typingSpeed;

        for (int i = 0; i < fullText.Length; i++)
        {
            textMeshPro.text += fullText[i];

            if (typingSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(typingSound, typingSoundVolume);
            }

            yield return new WaitForSeconds(delay);
        }

        typingCoroutine = null;
    }

    // Hoàn thành ngay lập tức hiệu ứng đánh máy, hiển thị toàn bộ văn bản.
    public void SkipTyping()
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }
        textMeshPro.text = fullText;
    }
}
