# ===============================================================
#  PyAnimalese – KR·EN·JP·ZH  (librosa time-stretch 버전 · 피치 유지)
# ---------------------------------------------------------------
#  · 반복 입력 (exit / quit → 종료)
#  · 초성 20개 .wav 파일 ➜ RAM 캐시
#  · 한국어 / 영어 / 일본어 / 중국어 문자 자동 인식
#  · librosa.effects.time_stretch 로 “속도↑ + 피치 유지”
#
#  요구 패키지
#  ------------------------------------------------------------
#       pip install numpy pydub simpleaudio jamo pronouncing
#                   pykakasi pypinyin librosa soundfile numba
#  ------------------------------------------------------------
#  *FFmpeg 설치 없이 동작합니다 (pydub이 WAV raw 데이터만 사용)*
# ===============================================================

import os, random, re, numpy as np
from functools import lru_cache
from typing import List

from pydub import AudioSegment
import simpleaudio as sa
import librosa                                     # ★ 피치 유지용
from jamo import h2j, j2hcj
import pronouncing
from pykakasi import kakasi
from pypinyin import lazy_pinyin, Style

# ============ 사용자 튜닝 ============ #
SPEED          = 4.0   # 평균 배속 (1.0 = 원속도)  → 4 이하 권장
RANDOM_JITTER  = 0.5   # ±50 % 속도 변동폭
VOWEL_STRETCH  = 2     # 모음(‘ㅇ’) 길이 배수
SRC = os.path.join(os.path.dirname(__file__), "sources", "Female1")
# ==================================== #

# ---------- 초성 목록 & 파일 매핑 ----------
CHOS  = ['ㄱ','ㄲ','ㄴ','ㄷ','ㄸ','ㄹ','ㅁ','ㅂ','ㅃ','ㅅ',
         'ㅆ','ㅇ','ㅈ','ㅉ','ㅊ','ㅋ','ㅌ','ㅍ','ㅎ',' ']
FILES = [f"{i+1:02}.wav" for i in range(20)]  # 01.wav ~ 20.wav
C2F   = dict(zip(CHOS, FILES))

# ---------- 영어 phoneme → 초성 ----------
PHON_TO_CHO = {
    'P':'ㅍ','B':'ㅂ','T':'ㄷ','D':'ㄷ','K':'ㄱ','G':'ㄱ',
    'F':'ㅍ','V':'ㅂ','TH':'ㄷ','DH':'ㄷ','S':'ㅅ','Z':'ㅅ',
    'SH':'ㅊ','ZH':'ㅊ','CH':'ㅊ','JH':'ㅈ',
    'M':'ㅁ','N':'ㄴ','NG':'ㅇ','L':'ㄹ','R':'ㄹ',
    'Y':'ㅈ','HH':'ㅎ','W':'ㅇ',
}
VOWELS = {'AA','AE','AH','AO','AW','AY','EH','ER','EY',
          'IH','IY','OW','OY','UH','UW'}

# ---------- 일본어 로마자 → 초성 ----------
ROMA_TO_CHO = {
    'ch':'ㅊ','sh':'ㅅ','ts':'ㅊ',
    'ky':'ㅋ','gy':'ㄱ','ny':'ㄴ','hy':'ㅎ','my':'ㅁ','ry':'ㄹ',
    'py':'ㅍ','by':'ㅂ','j':'ㅈ',
    'k':'ㅋ','g':'ㄱ','s':'ㅅ','z':'ㅈ','t':'ㄷ','d':'ㄷ',
    'n':'ㄴ','h':'ㅎ','b':'ㅂ','p':'ㅍ','m':'ㅁ',
    'y':'ㅇ','r':'ㄹ','w':'ㅇ'
}

# ---------- 중국어 병음 초성 → 초성 ----------
PINYIN_TO_CHO = {
    'zh':'ㅈ','ch':'ㅊ','sh':'ㅅ','z':'ㅈ','c':'ㅊ','s':'ㅅ',
    'j':'ㅈ','q':'ㅊ','x':'ㅅ',
    'b':'ㅂ','p':'ㅍ','m':'ㅁ','f':'ㅍ',
    'd':'ㄷ','t':'ㄷ','n':'ㄴ','l':'ㄹ',
    'g':'ㄱ','k':'ㅋ','h':'ㅎ',
    'r':'ㄹ'
}

# ---------- 1) 초성 음원 캐싱 ----------
print("초성 음원 캐싱중...", end="", flush=True)
PRELOAD = {
    cho: AudioSegment.from_wav(os.path.join(SRC, fn))
          .set_frame_rate(44100).set_sample_width(2).set_channels(1)
    for cho, fn in C2F.items()
}
print("완료")

def cho_seg(cho):   # helper
    return PRELOAD.get(cho)

# ---------- 2) 발음·로마자 캐시 ----------
@lru_cache(maxsize=10000)
def eng_phones(word):
    p = pronouncing.phones_for_word(word.lower())
    return p[0].split() if p else None

kks = kakasi()
@lru_cache(maxsize=10000)
def ja_romaji(text):
    return [d['hepburn'].lower() for d in kks.convert(text)]

@lru_cache(maxsize=10000)
def zh_pinyins(text):
    return lazy_pinyin(text, style=Style.NORMAL)

# ---------- 3) 속도 변조 (librosa time-stretch) ----------
ALPHA = 0.5        # 0.0~1.0 사이 원하는 값으로

def speedup(seg: AudioSegment, rate: float) -> AudioSegment:
    """
    Hybrid time-stretch + frame-rate warp
    피치 보정 비율 = ALPHA
    """
    if abs(rate - 1.0) < 1e-3:
        return seg

    # ----------------------------------
    # 1단계: librosa time-stretch (피치 보존)
    #   stretch 비율 = rate ** ALPHA
    # ----------------------------------
    stretch_rate = rate ** ALPHA
    y = np.array(seg.get_array_of_samples()).astype(np.float32) / 32768.0
    y = librosa.effects.time_stretch(y, rate=stretch_rate)
    y = np.clip(y, -1.0, 1.0)
    y_int16 = (y * 32767).astype(np.int16)
    seg_stretched = AudioSegment(
        y_int16.tobytes(),
        frame_rate=seg.frame_rate,
        sample_width=2,
        channels=seg.channels
    )

    # ----------------------------------
    # 2단계: frame-rate warp (피치 상승)
    #   warp 비율 = rate ** (1-ALPHA)
    # ----------------------------------
    warp_rate = rate ** (1.0 - ALPHA)
    warped = seg_stretched._spawn(
        seg_stretched.raw_data,
        overrides={'frame_rate': int(seg_stretched.frame_rate * warp_rate)}
    ).set_frame_rate(seg.frame_rate)

    return warped
def vary(seg):
    rate = SPEED * random.uniform(1 - RANDOM_JITTER, 1 + RANDOM_JITTER)
    # librosa 권장 범위 0.25~4.0 … SPEED 4 이하를 추천
    return speedup(seg, rate)

def stretch(seg, factor=1):
    """모음(ㅇ) 길이 늘이기"""
    if factor <= 1:
        return seg
    tail = 30  # ms
    out = seg
    for _ in range(factor - 1):
        out = out.append(seg[-tail:], crossfade=tail // 2)
    return out

# ---------- 4) 언어별 세그먼트 생성 ----------
def kor_segs(text):
    for ch in j2hcj(h2j(text)):
        seg = cho_seg(ch)
        if seg:
            seg = vary(seg)
            if ch == 'ㅇ':
                seg = stretch(seg, VOWEL_STRETCH)
            yield seg

def eng_segs(word):
    phs = eng_phones(word)
    if not phs:   # 발음 사전에 없으면 ‘ㅇ’ 길게
        yield vary(stretch(cho_seg('ㅇ'), VOWEL_STRETCH));  return
    for ph in phs:
        ph = re.sub(r'\d', '', ph)                # 강세 숫자 제거
        cho = PHON_TO_CHO.get(ph) or ('ㅇ' if ph in VOWELS else None)
        seg = cho_seg(cho) if cho else None
        if seg:
            seg = vary(seg)
            if cho == 'ㅇ':
                seg = stretch(seg, VOWEL_STRETCH)
            yield seg

# - 일본어: 로마자 → onset 분리
CON_CLU = ("ch","sh","ts","ky","gy","ny","hy","my","ry",
           "py","by","j")
VOWEL = "aeiou"
def split_mora(roma):
    i, n, res = 0, len(roma), []
    while i < n:
        for clu in CON_CLU:
            if roma.startswith(clu, i):
                onset = clu;  i += len(clu);  break
        else:
            onset = roma[i] if roma[i] in "bcdfghjklmnpqrstvwxyz" else ''
            if onset:
                i += 1
        if i < n and roma[i] in VOWEL:   # 모음 스킵
            i += 1
        res.append(onset)
        # final n
        if i < n and roma[i] == 'n' and (i+1==n or roma[i+1] not in VOWEL):
            res.append('n'); i += 1
    return res

def ja_segs(word):
    for roma in ja_romaji(word):
        for onset in split_mora(roma):
            cho = (ROMA_TO_CHO.get(onset)
                   or ('ㅇ' if onset == '' else 'ㄴ' if onset=='n' else None))
            seg = cho_seg(cho) if cho else None
            if seg:
                seg = vary(seg)
                if cho == 'ㅇ':
                    seg = stretch(seg, VOWEL_STRETCH)
                yield seg

def zh_segs(word):
    for syll in zh_pinyins(word):
        onset = next((pre for pre in ('zh','ch','sh')
                      if syll.startswith(pre)), '')
        if not onset:
            onset = syll[:1] if syll and syll[0].isalpha() else ''
        cho = PINYIN_TO_CHO.get(onset, 'ㅇ')
        seg = cho_seg(cho)
        if seg:
            seg = vary(seg)
            if cho == 'ㅇ':
                seg = stretch(seg, VOWEL_STRETCH)
            yield seg

# ---------- 5) 문장 → AudioSegment ----------
TOKEN = re.compile(
    r"[가-힣]+|"            # 한글
    r"[A-Za-z']+|"         # 영문
    r"[ぁ-んァ-ンー一-龯]+|" # 일본어
    r"[\u4E00-\u9FFF]+|"   # 한자(중국어)
    r"."                   # 그 외
)

def synth(text: str) -> AudioSegment:
    """문자열 → 합성 음성(AudioSegment)"""
    out = AudioSegment.silent(duration=0)
    for tk in TOKEN.findall(text):
        if re.fullmatch(r"[가-힣]+", tk):
            for s in kor_segs(tk): out += s
        elif re.fullmatch(r"[A-Za-z']+", tk):
            for s in eng_segs(tk): out += s
        elif re.fullmatch(r"[ぁ-んァ-ンー一-龯]+", tk):
            for s in ja_segs(tk):  out += s
        elif re.fullmatch(r"[\u4E00-\u9FFF]+", tk):
            for s in zh_segs(tk):  out += s
        # 기타 기호·스페이스는 무음 처리
    return out

# ---------- 6) 재생 ----------
def play(seg: AudioSegment):
    if len(seg) == 0:
        print("재생할 소리가 없습니다."); return
    sa.play_buffer(seg.raw_data,
                   num_channels=seg.channels,
                   bytes_per_sample=seg.sample_width,
                   sample_rate=seg.frame_rate).wait_done()

# ---------- 7) REPL ----------
if __name__ == "__main__":
    print("▶ PyAnimalese (librosa)  |  exit / quit → 종료")
    while True:
        txt = input("\n원본 문자열 입력> ").strip()
        if txt.lower() in {"exit", "quit"}:
            break
        audio = synth(txt)
        print(f"재생중: {txt}")
        play(audio)