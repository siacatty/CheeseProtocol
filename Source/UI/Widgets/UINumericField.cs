using UnityEngine;
using Verse;

namespace CheeseProtocol
{
    public static class UiNumericField
    {
        /// <summary>
        /// 숫자만 입력 가능(TextField 기반). 입력 중 빈칸 허용(그때 value=0).
        /// - non-digit은 즉시 제거
        /// - maxDigits로 자리수 제한 가능(0이면 제한 없음)
        /// - min/max로 value를 clamp
        /// </summary>
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