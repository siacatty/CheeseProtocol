using Verse;
using UnityEngine;
using System.Collections.Generic;
using System;
using RimWorld;

namespace CheeseProtocol
{
    public static class UIUtil
    {
        private static int _activeRangeId = -1;
        private static bool _dragMax = false;
        private static float _dragStartMouseX = 0f;
        private static bool _dragDirDecided = true;

        public readonly struct GUIStateScope : IDisposable
        {
            private readonly bool prevGUI;
            public GUIStateScope(bool enabled)
            {
                prevGUI = GUI.enabled;
                GUI.enabled = enabled;
            }
            public void Dispose() => GUI.enabled = prevGUI;
        }

        public static void AutoScrollView(
            Rect outerRect,
            ref Vector2 scrollPos,
            ref float cachedHeight,
            System.Func<Rect, float> drawContentAndReturnUsedHeight,
            bool registerAsInner = false
        )
        {
            // 프레임 틱
            ScrollWheelBlocker.TickFrame();

            // inner scrollview면 자기 영역을 등록(스크린 좌표)
            if (registerAsInner && Event.current.type == EventType.Layout)
                ScrollWheelBlocker.Register(outerRect);

            // 휠 처리 (inner 위면 outer는 먹지 않게)
            Event e = Event.current;
            if (e.type == EventType.ScrollWheel && outerRect.Contains(e.mousePosition))
            {
                if (!ScrollWheelBlocker.IsBlocked(e.mousePosition) || registerAsInner)
                {
                    float maxScroll = Mathf.Max(0f, cachedHeight - outerRect.height);
                    scrollPos.y += e.delta.y * 40f;
                    scrollPos.y = Mathf.Clamp(scrollPos.y, 0f, maxScroll);
                    e.Use();
                }
            }

            // viewRect는 "content 좌표(0,0)" 기준이 정석
            float contentH = Mathf.Max(cachedHeight, outerRect.height);
            Rect viewRect = new Rect(0f, 0f, outerRect.width - 16f, contentH);

            Widgets.BeginScrollView(outerRect, ref scrollPos, viewRect);

            float usedH = 0f;
            if (drawContentAndReturnUsedHeight != null)
                usedH = drawContentAndReturnUsedHeight(viewRect);

            // Layout에서만 캐시 갱신
            if (Event.current.type == EventType.Layout)
                cachedHeight = usedH;

            // 컨텐츠가 줄어든 경우 스크롤 위치 clamp
            float maxScrollNow = Mathf.Max(0f, cachedHeight - outerRect.height);
            if (scrollPos.y > maxScrollNow) scrollPos.y = maxScrollNow;

            Widgets.EndScrollView();
        }
        public static class ScrollWheelBlocker
        {
            private static int _lastFrame = -1;

            // 이번 프레임에 수집된 inner rect들 (스크린 좌표)
            private static readonly List<Rect> _current = new List<Rect>(32);

            // 바깥이 참고할 "마지막으로 확정된" rect들
            private static readonly List<Rect> _stable = new List<Rect>(32);

            /// <summary>
            /// 프레임 전환 감지해서 rect 버퍼를 안정화.
            /// (각 GUI 호출에서 자동으로 불리게 하면 됨)
            /// </summary>
            public static void TickFrame()
            {
                int f = Time.frameCount;
                if (f == _lastFrame) return;

                _lastFrame = f;

                _stable.Clear();
                _stable.AddRange(_current);
                _current.Clear();
            }

            /// <summary>
            /// inner ScrollView 영역(스크린 좌표)을 등록.
            /// </summary>
            public static void Register(Rect screenRect)
            {
                TickFrame();
                _current.Add(screenRect);
            }

            /// <summary>
            /// 현재 마우스가 inner 영역 위에 있다고 "판정"되면 true.
            /// (바깥 ScrollView는 이때 휠을 먹지 않도록)
            /// </summary>
            public static bool IsBlocked(Vector2 mousePos)
            {
                TickFrame();
                for (int i = 0; i < _stable.Count; i++)
                {
                    if (_stable[i].Contains(mousePos))
                        return true;
                }
                return false;
            }
        }

        public static Rect ContentToRootRect(Rect outerRect, Vector2 outerScroll, Rect innerInContent)
        {
            return new Rect(
                outerRect.x + innerInContent.x,
                outerRect.y + (innerInContent.y - outerScroll.y),
                innerInContent.width,
                innerInContent.height
            );
        }
        
        public static Rect RowWithHighlight(Rect rect, ref float curY, float height, Action<Rect> draw, float contract = 0f)
        {
            Rect row = new Rect(rect.x, curY, rect.width, height);
            curY+=height;
            Widgets.DrawHighlightIfMouseover(row);

            Rect content = contract > 0f ? row.ContractedBy(contract) : row;
            draw?.Invoke(content);
            return content;
        }

        public static Rect RowWithHighlightListing(Listing_Standard listing, float height, Action<Rect> draw, float contract = 0f)
        {
            Rect row = listing.GetRect(height);
            Widgets.DrawHighlightIfMouseover(row);

            Rect content = contract > 0f ? row.ContractedBy(contract) : row;
            draw?.Invoke(content);
            return content;
        }
        
        public static void RangeSlider(
            Rect rect,
            ref float minValue,
            ref float maxValue,
            float minLimit,
            float maxLimit,
            bool highlightRange = true,
            float roundTo = 0f,
            float handleW = 12f,
            float barH = 4f
        )
        {
            if (maxLimit <= minLimit) return;

            minValue = Mathf.Clamp(minValue, minLimit, maxLimit);
            maxValue = Mathf.Clamp(maxValue, minLimit, maxLimit);
            if (minValue > maxValue) minValue = maxValue;

            int id = GUIUtility.GetControlID(FocusType.Passive, rect);

            float pad = handleW * 0.5f;
            Rect track = new Rect(rect.x + pad, rect.center.y - barH * 0.5f, rect.width - pad * 2f, barH);

            float ToX(float v)
            {
                float t = Mathf.InverseLerp(minLimit, maxLimit, v);
                return Mathf.Lerp(track.xMin, track.xMax, t);
            }
            float FromX(float x)
            {
                float t = Mathf.InverseLerp(track.xMin, track.xMax, x);
                float v = Mathf.Lerp(minLimit, maxLimit, t);
                if (roundTo > 0f) v = Mathf.Round(v / roundTo) * roundTo;
                return Mathf.Clamp(v, minLimit, maxLimit);
            }

            float xMin = ToX(minValue);
            float xMax = ToX(maxValue);

            Rect hMin = new Rect(xMin - handleW * 0.5f, rect.y, handleW, rect.height);
            Rect hMax = new Rect(xMax - handleW * 0.5f, rect.y, handleW, rect.height);

            Widgets.DrawBoxSolid(track, new Color(1f, 1f, 1f, 0.18f));

            if (highlightRange)
            {
                Rect range = Rect.MinMaxRect(Mathf.Min(xMin, xMax), track.y, Mathf.Max(xMin, xMax), track.yMax);
                Widgets.DrawBoxSolid(range, new Color(1f, 1f, 1f, 0.35f));
            }

            Widgets.DrawBoxSolid(hMin.ContractedBy(1f), new Color(1f, 1f, 1f, 0.55f));
            Widgets.DrawBoxSolid(hMax.ContractedBy(1f), new Color(1f, 1f, 1f, 0.75f));

            Event e = Event.current;

            bool OverlapHandles() => hMin.Overlaps(hMax) || Mathf.Abs(xMin - xMax) <= 0.001f;

            if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition))
            {
                bool hitMin = hMin.Contains(e.mousePosition);
                bool hitMax = hMax.Contains(e.mousePosition);

                if (OverlapHandles())
                {
                    // NEW: 겹친 상태에서는 "방향"으로 결정 (일단 미결정 상태로 두고 Drag에서 확정)
                    _dragDirDecided = false;
                    _dragStartMouseX = e.mousePosition.x;

                    // 기본값은 max (너의 기존 의도 유지)
                    _dragMax = true;
                }
                else if (hitMin && !hitMax)
                {
                    _dragMax = false;
                    _dragDirDecided = true;
                }
                else if (hitMax && !hitMin)
                {
                    _dragMax = true;
                    _dragDirDecided = true;
                }
                else
                {
                    float dxMin = Mathf.Abs(e.mousePosition.x - xMin);
                    float dxMax = Mathf.Abs(e.mousePosition.x - xMax);
                    _dragMax = dxMax <= dxMin;
                    _dragDirDecided = true;
                }

                _activeRangeId = id;
                GUIUtility.hotControl = id;
                e.Use();
            }

            if (_activeRangeId == id && GUIUtility.hotControl == id)
            {
                if (e.type == EventType.MouseDrag && e.button == 0)
                {
                    // NEW: 겹친 상태에서 아직 미결정이면, 마우스 델타 방향으로 thumb 확정
                    if (!_dragDirDecided)
                    {
                        const float deadzonePx = 3f; // 너무 민감하면 4~6으로
                        float dx = e.mousePosition.x - _dragStartMouseX;

                        if (Mathf.Abs(dx) >= deadzonePx)
                        {
                            // 오른쪽(+): max, 왼쪽(-): min
                            _dragMax = dx >= 0f;
                            _dragDirDecided = true;
                        }
                        else
                        {
                            // deadzone 안에서는 아직 결정 안 하고 그냥 대기 (값 변경 X)
                            e.Use();
                            return;
                        }
                    }

                    float v = FromX(e.mousePosition.x);

                    if (_dragMax)
                        maxValue = Mathf.Max(v, minValue);
                    else
                        minValue = Mathf.Min(v, maxValue);

                    e.Use();
                }
                else if (e.type == EventType.MouseUp && e.button == 0)
                {
                    _activeRangeId = -1;
                    GUIUtility.hotControl = 0;
                    _dragDirDecided = true; // 다음 입력을 위해 리셋
                    e.Use();
                }
            }
        }
        public static void RangeSliderWrapperThingTier(Rect rect, ref float curY, float height, string label, ref QualityRange range, float baseMin=0f, float baseMax=1f, bool isPercentile=false, bool highlightMouseover = true, bool highlightRange = true, float roundTo = 0.01f)
        {
            float paddingX = 6f;
            float min = range.qMin;
            float max = range.qMax;
            Rect row = new Rect(rect.x, curY, rect.width, height);
            curY+=height;

            if (highlightMouseover)
                Widgets.DrawHighlightIfMouseover(row);

            SplitVerticallyByRatio(row, out Rect labelRect, out Rect sliderWrapRect, 0.4f, paddingX);
            var cols = new List<Rect>(3);
            SplitVerticallyByRatios(sliderWrapRect, new float[] { 0.15f, 0.7f, 0.15f }, paddingX, cols);
            Rect minRect = cols[0];
            Rect sliderRect = cols[1];
            Rect maxRect = cols[2];
            DrawCenteredText(labelRect, label, TextAlignment.Left);
            RangeSlider(sliderRect, ref min, ref max, baseMin, baseMax, highlightRange: highlightRange, roundTo: roundTo);
            int minInt = Mathf.RoundToInt(min);
            int maxInt = Mathf.RoundToInt(max);
            string minString = minInt switch
            {
                0 => "끔찍",
                1 => "빈약",
                2 => "평범",
                3 => "상급",
                4 => "완벽",
                5 => "걸작",
                6 => "전설",
                _ => "미정"
            };
            string maxString = maxInt switch
            {
                0 => "끔찍",
                1 => "빈약",
                2 => "평범",
                3 => "상급",
                4 => "완벽",
                5 => "걸작",
                6 => "전설",
                _ => "미정"
            };
            DrawCenteredText(minRect, minString);
            DrawCenteredText(maxRect, maxString);
            range = QualityRange.init(min, max);
        }
        public static void RangeSliderWrapperTechLevel(Rect rect, ref float curY, float height, string label, ref QualityRange range, float baseMin=0f, float baseMax=1f, bool isPercentile=false, bool highlightMouseover = true, bool highlightRange = true, float roundTo = 0.01f)
        {
            float paddingX = 6f;
            float min = range.qMin;
            float max = range.qMax;
            Rect row = new Rect(rect.x, curY, rect.width, height);
            curY+=height;

            if (highlightMouseover)
                Widgets.DrawHighlightIfMouseover(row);

            SplitVerticallyByRatio(row, out Rect labelRect, out Rect sliderWrapRect, 0.4f, paddingX);
            var cols = new List<Rect>(3);
            SplitVerticallyByRatios(sliderWrapRect, new float[] { 0.15f, 0.7f, 0.15f }, paddingX, cols);
            Rect minRect = cols[0];
            Rect sliderRect = cols[1];
            Rect maxRect = cols[2];
            DrawCenteredText(labelRect, label, TextAlignment.Left);
            RangeSlider(sliderRect, ref min, ref max, baseMin, baseMax, highlightRange: highlightRange, roundTo: roundTo);
            int minInt = Mathf.RoundToInt(min);
            int maxInt = Mathf.RoundToInt(max);
            string minString = minInt switch
            {
                0 => "미정",
                1 => "동물",
                2 => "신석기",
                3 => "중세",
                4 => "산업",
                5 => "우주",
                6 => "초월",
                7 => "아코텍",
                _ => "미정"
            };
            string maxString = maxInt switch
            {
                0 => "미정",
                1 => "동물",
                2 => "신석기",
                3 => "중세",
                4 => "산업",
                5 => "우주",
                6 => "초월",
                7 => "아코텍",
                _ => "미정"
            };
            DrawCenteredText(minRect, minString);
            DrawCenteredText(maxRect, maxString);
            range = QualityRange.init(min, max);
        }
        public static void RangeSliderWrapper(Rect rect, ref float curY, float height, string label, ref QualityRange range, float baseMin=0f, float baseMax=1f, bool isPercentile=false, bool highlightMouseover = true, bool highlightRange = true, float roundTo = 0.01f)
        {
            float paddingX = 6f;
            float min = range.qMin;
            float max = range.qMax;
            Rect row = new Rect(rect.x, curY, rect.width, height);
            curY+=height;

            if (highlightMouseover)
                Widgets.DrawHighlightIfMouseover(row);

            SplitVerticallyByRatio(row, out Rect labelRect, out Rect sliderWrapRect, 0.4f, paddingX);
            var cols = new List<Rect>(3);
            SplitVerticallyByRatios(sliderWrapRect, new float[] { 0.15f, 0.7f, 0.15f }, paddingX, cols);
            Rect minRect = cols[0];
            Rect sliderRect = cols[1];
            Rect maxRect = cols[2];
            DrawCenteredText(labelRect, label, TextAlignment.Left);

            RangeSlider(sliderRect, ref min, ref max, baseMin, baseMax, highlightRange: highlightRange, roundTo: roundTo);

            string minString = isPercentile? $"{Mathf.RoundToInt(min*100f)}%" : Mathf.RoundToInt(min).ToString();
            string maxString = isPercentile? $"{Mathf.RoundToInt(max*100f)}%" : Mathf.RoundToInt(max).ToString();
            DrawCenteredText(minRect, minString);
            DrawCenteredText(maxRect, maxString);
            range = QualityRange.init(min, max);
        }
        public static void SplitHorizontallyByRatios(
            Rect rect,
            IList<float> ratios,
            float margin,
            List<Rect> outRects)
        {
            outRects.Clear();
            if (ratios == null || ratios.Count == 0) return;

            float totalMargin = margin * (ratios.Count - 1);
            float usableH = Mathf.Max(0f, rect.height - totalMargin);

            float sum = 0f;
            for (int i = 0; i < ratios.Count; i++)
                sum += Mathf.Max(0f, ratios[i]);

            if (sum <= 0f) return;

            float y = rect.y;

            for (int i = 0; i < ratios.Count; i++)
            {
                float r = Mathf.Max(0f, ratios[i]) / sum;
                float h = (i == ratios.Count - 1)
                    ? (rect.yMax - y)
                    : usableH * r;

                var part = new Rect(rect.x, y, rect.width, Mathf.Max(0f, h));
                outRects.Add(part);

                y = part.yMax + margin;
            }
        }
        public static void SplitVerticallyByRatios(
            Rect rect,
            IList<float> ratios,
            float margin,
            List<Rect> outRects)
        {
            outRects.Clear();
            if (ratios == null || ratios.Count == 0) return;

            // margin 때문에 실제 사용 가능한 폭
            float totalMargin = margin * (ratios.Count - 1);
            float usableW = Mathf.Max(0f, rect.width - totalMargin);

            // ratios 합으로 정규화(합이 1이 아니어도 동작)
            float sum = 0f;
            for (int i = 0; i < ratios.Count; i++)
                sum += Mathf.Max(0f, ratios[i]);

            if (sum <= 0f) return;

            float x = rect.x;

            for (int i = 0; i < ratios.Count; i++)
            {
                float r = Mathf.Max(0f, ratios[i]) / sum;
                float w = (i == ratios.Count - 1)
                    ? (rect.xMax - x)
                    : usableW * r;

                var part = new Rect(x, rect.y, Mathf.Max(0f, w), rect.height);
                outRects.Add(part);

                x = part.xMax + margin;
            }
        }
        public static void SplitVerticallyByRatio(
            Rect rect,
            out Rect left,
            out Rect right,
            float leftRatio,
            float margin)
        {
            leftRatio = Mathf.Clamp01(leftRatio);

            float leftWidth = (rect.width - margin) * leftRatio;

            left = new Rect(rect.x, rect.y, leftWidth, rect.height);
            right = new Rect(
                rect.x + leftWidth + margin,
                rect.y,
                rect.width - leftWidth - margin,
                rect.height
            );
        }
        public static void SplitHorizontallyByRatio(
            Rect rect,
            out Rect top,
            out Rect bottom,
            float topRatio,
            float margin)
        {
            topRatio = Mathf.Clamp01(topRatio);

            float topHeight = (rect.height - margin) * topRatio;

            top = new Rect(rect.x, rect.y, rect.width, topHeight);

            bottom = new Rect(
                rect.x,
                rect.y + topHeight + margin,
                rect.width,
                rect.height - topHeight - margin
            );
        }
        public static void SplitVerticallyByWidth(
            Rect rect,
            out Rect left,
            out Rect right,
            float leftWidth,
            float margin)
        {
            leftWidth = Mathf.Clamp(leftWidth, 0f, rect.width - margin);

            left = new Rect(rect.x, rect.y, leftWidth, rect.height);
            right = new Rect(
                rect.x + leftWidth + margin,
                rect.y,
                rect.width - leftWidth - margin,
                rect.height
            );
        }
        public static void SplitHorizontallyByHeight(
            Rect rect,
            out Rect top,
            out Rect bottom,
            float topHeight,
            float margin)
        {
            topHeight = Mathf.Clamp(topHeight, 0f, rect.height - margin);

            top = new Rect(rect.x, rect.y, rect.width, topHeight);
            bottom = new Rect(
                rect.x,
                rect.y + topHeight + margin,
                rect.width,
                rect.height - topHeight - margin
            );
        }
        public static Rect ShrinkRect(Rect rect, float left=0, float right=0, float top=0, float bottom=0, TextAlignment alignRect = TextAlignment.Center)
        {
            return new Rect(
                rect.x + left,
                rect.y + top,
                rect.width - left - right,
                rect.height - top - bottom
            );
        }
        public static Rect ResizeRectAligned(
            Rect rect,
            float targetWidth,
            float targetHeight,
            TextAlignment alignRect = TextAlignment.Center)
        {
            float newWidth  = rect.width  > targetWidth  ? targetWidth  : rect.width;
            float newHeight = rect.height > targetHeight ? targetHeight : rect.height;

            float x;
            switch (alignRect)
            {
                case TextAlignment.Left:
                    x = rect.x;
                    break;
                case TextAlignment.Right:
                    x = rect.x + (rect.width - newWidth);
                    break;
                case TextAlignment.Center:
                default:
                    x = rect.x + (rect.width - newWidth) * 0.5f;
                    break;
            }

            // 세로 가운데는 유지
            float y = rect.y + (rect.height - newHeight) * 0.5f;

            return new Rect(x, y, newWidth, newHeight);
        }

        public static void DrawCenteredText(Rect rect, string text, TextAlignment align = TextAlignment.Center, GameFont font = GameFont.Small, Color? color = null)
        {
            var prevFont = Verse.Text.Font;
            var prevColor = GUI.color;

            Verse.Text.Font = font;
            if (color.HasValue)
                GUI.color = color.Value;

            Vector2 size = Text.CalcSize(text);

            float x = rect.x;
            switch (align)
            {
                case TextAlignment.Center:
                    x = rect.x + (rect.width - size.x) * 0.5f;
                    break;
                case TextAlignment.Right:
                    x = rect.x + rect.width - size.x;
                    break;
                case TextAlignment.Left:
                default:
                    x = rect.x;
                    break;
            }
            float y = rect.y + (rect.height - size.y) * 0.5f;

            Widgets.Label(new Rect(x, y, size.x, size.y), text);

            GUI.color = prevColor;
            Verse.Text.Font = prevFont;
        }
        public static void IntFieldDigitsOnly(
            Rect rect,
            ref int value,
            ref string buffer,
            int min,
            int max,
            int maxDigits=9)
        {
            if (buffer == null) buffer = value.ToString();

            // 1) 입력
            string newBuf = Widgets.TextField(rect, buffer);

            // 2) 즉시 정제: 숫자만 남기기
            if (newBuf.Length > 0)
            {
                bool changed = false;
                var chars = newBuf.ToCharArray();

                int write = 0;
                for (int read = 0; read < chars.Length; read++)
                {
                    char c = chars[read];
                    if (c >= '0' && c <= '9')
                    {
                        chars[write++] = c;
                    }
                    else
                    {
                        changed = true;
                    }
                }

                if (changed)
                    newBuf = new string(chars, 0, write);
            }

            // 3) 자리수 제한 (선택)
            if (maxDigits > 0 && newBuf.Length > maxDigits)
                newBuf = newBuf.Substring(0, maxDigits);

            buffer = newBuf;

            // 4) value 동기화
            if (string.IsNullOrEmpty(buffer))
            {
                value = 0;
                return;
            }
            if (int.TryParse(buffer, out int parsed))
            {
                value = Mathf.Clamp(parsed, min, max);
                if (value != parsed)
                    buffer = value.ToString();
            }
            else
            {
                value = max;
                buffer = value.ToString();
            }
        }
    }
}