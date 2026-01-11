using UnityEngine;
using Verse;

public class PreviewDirtyDebouncer
{
    private int _lastHash;
    private bool _dirty;
    private float _dirtySinceRealtime;
    public float DebounceSec = 0.20f;

    public void Tick(int hashProvider, System.Action invoke)
    {
        if (hashProvider != _lastHash)
        {
            _lastHash = hashProvider;
            _dirty = true;
            _dirtySinceRealtime = Time.realtimeSinceStartup;
        }
        if (!_dirty) return;

        bool mouseReleased = (Event.current.type == EventType.MouseUp);
        float dt = Time.realtimeSinceStartup - _dirtySinceRealtime;

        if (mouseReleased || dt >= DebounceSec)
        {
            invoke?.Invoke();
            _dirty = false;
        }
    }
    ///외부에서 강제로 dirty 처리하고 싶을 때(탭 전환 등)
    public void MarkDirty()
    {
        _dirty = true;
        _dirtySinceRealtime = Time.realtimeSinceStartup;
    }

    public void Reset(int currentHash)
    {
        _lastHash = currentHash;
        _dirty = false;
        _dirtySinceRealtime = 0f;
    }
}