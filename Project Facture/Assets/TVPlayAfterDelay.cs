using System.Collections;
using UnityEngine;
using UnityEngine.Video;

[RequireComponent(typeof(VideoPlayer))]
public class TVPlayAfterDelay : MonoBehaviour
{
    public float delaySeconds = 10f;

    [Header("Video in StreamingAssets (filename.mp4)")]
    public string streamingAssetsFileName = "ai_broadcast.mp4";

    private VideoPlayer vp;

    void Awake()
    {
        vp = GetComponent<VideoPlayer>();
        vp.playOnAwake = false;
        vp.url = System.IO.Path.Combine(Application.streamingAssetsPath, streamingAssetsFileName);
    }

    IEnumerator Start()
    {
        yield return new WaitForSeconds(delaySeconds);

        vp.Prepare();
        while (!vp.isPrepared) yield return null;

        vp.Play();
    }
}