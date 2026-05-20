# Drone Agent - Odul Sistemi Raporu

Bu rapor, `AgentDroneController.cs` icerisindeki mevcut odul/ceza sistemini inceler;
her bir kalemin amacini, etkisini ve onerilen degerleri ozetler. Sonunda
ek olarak dusunulebilecek yeni oduller ve cikarilabilecek gereksiz olanlar listelenir.

---

## 1. Mevcut Odul Seti - Ozet Tablo

### Sparse (Episode-sonu / Olaya bagli) Oduller

| Kalem                  | Varsayilan | Tip      | Tetikleyici                          | Episode sonu? |
|------------------------|-----------:|----------|--------------------------------------|---------------|
| `waypointReward`       |    `+1.0` | Pozitif  | Aktif waypoint'ten dogru yonde gecis | Hayir         |
| `completionBonus`      |    `+2.0` | Pozitif  | Son waypoint gecildi                 | Evet          |
| `crashPenalty`         |    `-1.0` | Negatif  | Ground/World Object ile temas        | Evet          |
| `tiltPenalty`          |    `-1.0` | Negatif  | 45 derece ustu egim (opsiyonel)      | Evet          |
| `belowGroundPenalty`   |    `-1.0` | Negatif  | Y ekseni < 0                         | Evet          |
| `timeoutPenalty`       |    `-0.5` | Negatif  | MaxStep limitine ulasilmasi          | Evet          |

### Dense (Her FixedUpdate adiminda) Oduller

| Kalem                    | Varsayilan | Formul                                               |
|--------------------------|-----------:|------------------------------------------------------|
| `distanceRewardScale`    |    `0.02` | `(prevDist - curDist) * scale`                       |
| `alignmentRewardScale`   |    `0.01` | `dot(velocity_normalized, toWaypoint_normalized) * scale` |
| `timePenalty`            |   `0.001` | Her adim `-timePenalty`                              |

---

## 2. Her Odul - Detayli Analiz

### 2.1. `waypointReward = +1.0`
- **Amac:** Ana gorev sinyali. Ajana "dogru yoldasin" der.
- **Tetikleyici:** Dronun aktif waypoint collider'ina dogru yonde girmesi.
- **Neden bu deger:** 1.0 standart bir gorev odulu; diger odullerin buyuklugu buna gore olceklenmis.
- **Artir:** Ajan waypoint'lere yaklasmiyor, intermediate odullerle yetiniyor.
- **Azalt:** Ajan her pahasina waypoint'e dalmaya calisiyor, carpiyor.
- **Risk:** Cok buyuk olursa (ornegin 10+) ajan sadece ilk birkac waypoint'i hedefleyip gerisine bakmaz.

### 2.2. `completionBonus = +2.0`
- **Amac:** Tum parkuru bitirmeyi ekstra odullendir. Tek bir "bonus" sonsa ajanin son waypoint'e yonelmesini tesvik eder.
- **Tetikleyici:** Son waypoint ile ayni kare - episode biter.
- **Problem:** 2.0 gorece dusuk. Eger track 20 waypoint ise toplam sparse = 20 * 1.0 + 2.0 = 22. Bonus oraninda sadece %10 etki yapar.
- **Oneri:** Parkur uzadikca buyutun. Kaba kural: `completionBonus = waypointReward * waypointCount * 0.25`.
  - 10 waypoint -> 2.5
  - 20 waypoint -> 5.0
  - 50 waypoint -> 12.5

### 2.3. `crashPenalty = -1.0`
- **Amac:** Ajanin engellerden/yerden kacinmasini ogretir.
- **Tetikleyici:** Tag'i `Ground` veya `World Object` olan objelerle `OnCollisionEnter`.
- **Dikkat:** Mutlak degeri waypoint odulune yakin olmali. `|-1.0|` ve `+1.0` dengede.
- **Artir (mutlak):** Ajan sik sik carpiyor. -2.0 veya -3.0 deneyin.
- **Azalt:** Ajan korkak kaliyor, hareket etmiyor (genelde dense oduller hareketi tesvik eder, bu nadir durum).

### 2.4. `tiltPenalty = -1.0` (sadece `enableTiltCheck = true` ise)
- **Amac:** Angle mode egitiminde dronu duz tutmak.
- **Acro modda:** KAPALI olmali (`enableTiltCheck = false`). Acro'da flip/inversion dogal.
- **Oneri:** Acro icin `enableTiltCheck = false`. Angle mode icin `max = 60-70` deneyin (45 cok agresif olabilir).

### 2.5. `belowGroundPenalty = -1.0`
- **Amac:** Collider'i olmayan zemin durumlarinda (terrain mesh bosluklari, despawn bolgeleri) guvenlik agi.
- **Tetikleyici:** `transform.position.y < 0` her FixedUpdate'de kontrol.
- **Dikkat:** Eger spawn point y=0 civarindaysa hemen tetikleniyor olabilir. Spawn'i y=5+ yapin.
- **Not:** `crashPenalty` ile ayni anda tetiklenebilir (zemin delmesi), bu cift ceza olur - sorun degil ama beklenmedik.

### 2.6. `timeoutPenalty = -0.5`
- **Amac:** Ajanin havada hoverlayip zamani bosa harcamasini engellemek.
- **Tetikleyici:** `StepCount >= MaxStep - 1` oldugunda verilir, ML-Agents episodu bitirir.
- **-0.5 neden:** Crash'ten daha hafif. "Zaman dolmasi, carpistan daha iyi ama tamamlamadan daha kotu" hiyerarsisini kurar.
- **Artir (mutlak):** Ajan hover yapip hicbir sey yapmiyor. -1.0'a cikarin.
- **Azalt:** Ajan zaman baskisi ile agresif ucup carpiyor.

### 2.7. `distanceRewardScale = 0.02`
- **Amac:** Her adim ajana "hedefe biraz daha yaklastim/uzaklastim" sinyali verir. Sparse waypoint odulu seyrek geldiginde gradient saglar.
- **Formul:** `(prevDist - curDist) * 0.02` - yaklasirsa pozitif, uzaklasirsa negatif.
- **Kumulatif analiz:** Dron 50m uzaktayken 5m'ye yaklasirsa toplam = 45m * 0.02 = **+0.9**. Yani tek bir waypoint'e tam yaklasma yaklasik `waypointReward` kadar bir sinyal verir. Guzel denge.
- **Artir:** Ajan yonunu bulamiyor, rastgele geziyor. 0.05'e cikarin.
- **Azalt:** Ajan waypoint'i tam gecmek yerine ona yaklasip uzaklasmaya baslar (reward hacking).

### 2.8. `alignmentRewardScale = 0.01`
- **Amac:** Hiz vektorunun waypoint yonune hizalanmasini odullendir. Ajanin "hedefe dogru yuzu donuk" ucmasini tesvik eder.
- **Formul:** `dot(v_hat, hedef_hat) * 0.01`. [-0.01, +0.01] araliginda adim basi.
- **Dikkat:** Sureli oldugu icin uzun episodlarda birikebilir. 1000 adim x 0.01 = 10 (cok yuksek). Ancak pratikte 0.5-0.7 ortalama dot ile 5-7'ye kadar cikar.
- **Artir:** Ajan hedefe dogru ama yan yan ucuyor (tipik drone probleminde zararsiz ama ogrenmeyi yavaslatir).
- **Azalt:** Ajan sadece duz ucup viraj yapamiyorsa azaltin.

### 2.9. `timePenalty = 0.001`
- **Amac:** Her adim kucuk negatif; gorevi cabuk bitirme baskisi.
- **Kumulatif:** 5000 adim (MaxStep) * 0.001 = -5.0. **Bu cok buyuk.** Ajan 5000 adim ucarsa toplam time penalty -5.0, oysa 20 waypoint + completion = +22.0. Yine de pozitif ama marji daraltiyor.
- **Oneri:** 0.0005'e dusurun veya MaxStep'i 3000'e kisin. Yoksa uzun parkurlarda ajan paniklenebilir.

---

## 3. Odul Buyukluk Analizi - Dengeleme

### Tipik Basarili Episode (20 waypoint'lik track, 800 adim)
```
+20 waypoint (20 * 1.0)            = +20.0
+completion bonus                  = +2.0
+distance reward (cumulative)      ~ +4.0   (waypointe yaklasmalar)
+alignment reward (0.5 avg)        ~ +4.0   (800 * 0.5 * 0.01)
-time penalty (800 * 0.001)        = -0.8
-----------------------------------------
TOPLAM                             ~ +29.2
```

### Tipik Basarisiz Episode (carpma, 300 adim)
```
+5 waypoint                        = +5.0
+distance/alignment                ~ +2.0
-time penalty (300 * 0.001)        = -0.3
-crashPenalty                      = -1.0
-----------------------------------------
TOPLAM                             ~ +5.7
```

### Kotu Episode (hover, timeout, 5000 adim)
```
+0 waypoint                        = 0.0
+distance (hicbir yere gitmiyor)   ~ 0.0
+alignment (velocity zero)         ~ 0.0
-time penalty (5000 * 0.001)       = -5.0
-timeoutPenalty                    = -0.5
-----------------------------------------
TOPLAM                             ~ -5.5
```

**Degerlendirme:** Basarili episode toplami (~+29), carpan episodden (~+5.7) ve hover episodden (-5.5) belirgin sekilde ayrisiyor. Gradient iyi.

**Sorun:** Basarisiz episode'da bile toplam pozitif (5.7). Bu "deneme yapmak icin motive edici" ama agent konforlu bir 'yariya kadar giderim sonra carpma riski alirim' stratejisine girebilir. `crashPenalty`'yi -2.0'a cikarmak bu yari-stratejiyi bozar.

---

## 4. Onerilen Degisiklikler (Oncelik Sirasi)

### Yuksek Oncelik

1. **`completionBonus`'u parkur uzunluguna gore olcekleyin**
   ```
   completionBonus = waypointCount * 0.25   // 20 waypoint -> 5.0
   ```
   Nedeni: mevcut 2.0, uzun parkurlarda onemsiz kaliyor.

2. **`crashPenalty`'yi -2.0'a cikarin**
   Nedeni: Yarisi bitirip carpma stratejisinin ceremesini artirir.

3. **Acro mod icin `enableTiltCheck = false` emin olun**
   Zaten default olarak kapali ama Inspector'da kontrol edin.

### Orta Oncelik

4. **`timePenalty`'yi 0.0005'e dusurun**
   MaxStep = 5000 olunca toplam baski -5.0'dan -2.5'a dusecek.

5. **Yeni odul: `progressReward` (waypoint-kesim ilerleme odulu)**
   Ajanin o ana kadar hic gecmedigi mesafeye ulasmasi icin bir defalik odul.
   Bu sayede "ileri gidip geri donerek reward farm'lama" davranisi engellenir.
   Pseudokod:
   ```csharp
   float currentProgress = trackProgress();  // 0..1
   if (currentProgress > maxProgressReached)
   {
       AddReward((currentProgress - maxProgressReached) * progressRewardScale);
       maxProgressReached = currentProgress;
   }
   ```

### Dusuk Oncelik (deneysel)

6. **Yeni ceza: `angularVelocityPenalty`**
   Acro'da spinning prevention yok. Ajan ogrenirken surekli donebilir.
   ```csharp
   AddReward(-rb.angularVelocity.sqrMagnitude * 0.0001f);
   ```
   Dikkatli olun: cok buyukse ajan viraj yapmaktan cekinir.

7. **Yeni odul: `smoothnessReward`**
   Action degisiminin kucuk olmasini odullendir (titremeyi azalt).
   ```csharp
   Vector4 deltaAction = currentAction - previousAction;
   AddReward(-deltaAction.sqrMagnitude * 0.0005f);
   ```

8. **Yeni odul: `speedReward`**
   Belirli bir min-hizin uzerinde ucmayi hafifce odullendir.
   Acro egitiminde ajanin hizli ucmasini tesvik eder.
   ```csharp
   if (rb.velocity.magnitude > minSpeed)
       AddReward(speedRewardScale);  // ornek: 0.0005
   ```

---

## 5. Cikarilabilecek / Degistirilebilecek Oduller

### `tiltPenalty` - acro'da anlamsiz
Acro modda flip'ler normal. `enableTiltCheck = false` yapin - bu kalem artik uygulanmiyor. Inspector'da birakin (angle mode'a dondugunuzde kullanabilirsiniz).

### `belowGroundPenalty` - spawn'a dikkat
Sorunlu degil ama spawn'inizi `y = 5+` yaparsaniz ceza anlamli hale gelir. Yoksa ajan her episode'da -1.0 ile basliyor olabilir.

### `alignmentRewardScale` - cok sadik olmayin
Bu genelde istenen davranis ama "her pahasina hedefe burnu donuk" olmak viraj almasini zorlastirir. Eger ajan viraj'da kafa karisiyorsa 0.01 -> 0.005'e dusurun.

---

## 6. Ogrenme Egrisi Takibi (TensorBoard)

Egitim sirasinda su metrikleri izleyin:

| Metrik                                  | Ne anlama gelir                              |
|-----------------------------------------|----------------------------------------------|
| `Environment/Cumulative Reward`         | Toplam episode odulu - yukselmeli           |
| `Environment/Episode Length`            | Episode uzunlugu - orta (2000-4000) olmali  |
| `Losses/Policy Loss`                    | Politika ogrenmesi - inisli cikisli olmali  |
| `Losses/Value Loss`                     | Deger tahmini - yavas yavas dusmeli         |
| `Policy/Entropy`                        | Aksiyon cesitligi - zamanla dusmeli         |

**Uyari isaretleri:**
- Reward platolasti ama hic yeni waypoint gecmiyor -> dense oduller yeterli degil, `waypointReward`'u artir.
- Episode length = MaxStep her zaman -> ajan hover yapiyor, `timePenalty` artir.
- Reward dalgalanmali ama ortalamada artiyor -> normal, sabirli olun.
- Reward hic artmiyor -> `learning_rate` yuksek olabilir, 1.0e-4'e dusurun.

---

## 7. Ozet - Onerilen Inspector Ayarlari (Acro egitimi)

```
Sparse Rewards:
  Waypoint Reward:         1.0
  Completion Bonus:        5.0     (20 waypoint track varsayar)
  Crash Penalty:          -2.0     (-1.0 yerine)
  Tilt Penalty:           -1.0     (kullanilmiyor, enableTiltCheck=false)
  Timeout Penalty:        -0.5
  Below Ground Penalty:   -1.0

Dense Rewards:
  Distance Reward Scale:   0.02
  Alignment Reward Scale:  0.005   (0.01 yerine, virajlar icin)
  Time Penalty:            0.0005  (0.001 yerine)

Episode Settings:
  Enable Tilt Check:       false   (ACRO)
  Max Step:                5000
```

Bu konfigurasyon ile ogrenme ortalama 2-5 milyon adimda stabilize olmalidir
(basit track icin). Karmasik track'ler icin 10M+ adim gerekebilir.
