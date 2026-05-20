# DroneSIM - Proje Kapsam Raporu

## Genel Bakis

DroneSIM, Unity oyun motoru uzerinde gelistirilmis bir **drone ucus simulatorudur**. Proje, gercekci fizik tabanli drone ucusu, yaris mekanigi ve makine ogrenmesi (ML-Agents) tabanli otonom drone egitimini bir arada sunmaktadir.

## Temel Ozellikler

### 1. Drone Ucus Fizigi
- **PID tabanli stabilizasyon** sistemi ile gercekci ucus modeli
- Uc farkli ucus modu destegi:
  - **Angle Mode**: Otomatik dengeleme, cubuk aci belirler
  - **Acro Mode**: Oran tabanli, cubuk donus hizi belirler
  - **Horizon Mode**: Hibrit mod, merkezde dengeleme, ucta acro
- Kutle, itki, suruklenme (drag), donus hizlari gibi ayarlanabilir fizik parametreleri

### 2. Coklu Giris Yontemi
- **Klavye** ile kontrol
- **Joystick** destegi (Mode 2 kontrol semasi)
- **UDP uzerinden harici kontrol** (port 9050) - harici kumanda veya yazilimlarla entegrasyon imkani

### 3. Drone Secim ve Konfigurasyon Sistemi
- ScriptableObject tabanli drone konfigurasyon sistemi (`DroneConfig`)
- 5 farkli drone modeli: AeroScout S2, Banshee RS, Phantom FX, SkyLark Trainer, Vortex 5
- Her drone icin zorluk seviyesi (Easy/Medium/Hard), kutle, itki, cevik, stabilite gibi ozellikler
- Drone secim ekrani: kart tabanli UI, detay paneli, ozellik barlari

### 4. Yaris Sistemi
- Gate (kapi) tabanli parkur yarisi
- Zamanlayici ve sira takibi
- Kapidan maksimum uzaklik kontrolu ve "sinyal kaybi" mekanigi
- Mesafe HUD gosterimi ve minimap destegi

### 5. ML-Agents ile Otonom Drone Egitimi
- Unity ML-Agents entegrasyonu
- Otonom drone agenti (`AgentDroneController`) - waypoint'ler uzerinden ucus ogrenme
- Paylasimli pist uzerinde coklu drone ayni anda egitim gorebilir
- Pist olusturucu (`AgentTrackGenerator`) ve waypoint sistemi
- Egitim yoneticisi (`AgentTrainingManager`) ve zaman olcegi kontrolu

### 6. Kullanici Arayuzu
- Ana menu ve sahne gecisleri
- Duraklatma menusu
- Ayarlar menusu (opsiyonlar)
- Stick gorsellestiricisi (kontrol girdi geri bildirimi)
- Cizgi rehberi (guideline) gorsellestiricisi

## Sahne Yapisi

| Sahne | Aciklama |
|-------|----------|
| Menu | Ana menu ve drone secim ekrani |
| SampleScene | Ana ucus / yaris sahnesi |
| phase1 | ML-Agents egitim sahnesi |

## Teknik Altyapi

- **Motor**: Unity (C#)
- **Fizik**: Rigidbody tabanli, PID kontrol donguleri
- **AI**: Unity ML-Agents (pekistirmeli ogrenme)
- **UI**: TextMesh Pro, Unity UI sistemi
- **Ag**: UDP soket iletisimi (harici kontrol icin)
- **Arazi**: Unity Terrain sistemi (3 farkli terrain asset)

## Ozet

DroneSIM, hem manuel ucus deneyimi hem de yapay zeka destekli otonom ucus egitimi sunan kapsamli bir drone simülatorudur. Farkli beceri seviyelerine uygun drone modelleri, coklu kontrol yontemleri ve yaris mekanigi ile hem egitim hem de eglence amacli kullanima uygundur.
