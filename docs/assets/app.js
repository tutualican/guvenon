(() => {
  "use strict";

  const REPO = "tutualican/guvenon";
  const REPO_URL = `https://github.com/${REPO}`;
  const SUPPORTED = ["en", "de", "tr"];
  const STORAGE_KEY = "guvenon.lang";

  const I18N = {
    en: {
      metaTitle: "Guven-ON — Privacy that just works",
      metaDescription:
        "Guven-ON adds opt-in DNS and local-network enforcement for untrusted Wi-Fi on Windows and Linux, stays quiet in the background, and speaks English, German and Turkish.",
      skip: "Skip to content",
      navWhy: "Why",
      navHow: "How it works",
      navPrivacy: "Privacy",
      navFaq: "FAQ",
      navDownload: "Download",
      heroBadge: "Windows 11 · Linux alpha · Preview",
      heroTitle1: "Privacy that",
      heroTitle2: "just works.",
      heroSub:
        "Guven-ON is built to block DNS leaks and local-network exposure on untrusted Wi-Fi — quietly, in the background. No account. No tunnel. No babysitting.",
      heroDownload: "Download for Windows",
      heroHow: "See how it works",
      heroMicro: "Preview build · opt-in protection · Windows 11 22H2+ · Linux .deb alpha",
      mockPreview: "Preview",
      mockStatus: "Protection ready",
      mockStatusSub: "Service, tray and opt-in enforcement installed",
      mockNetwork: "Wi-Fi",
      mockNetworkSub: "CoffeeShop_Guest",
      mockDns: "DNS policy",
      mockLan: "LAN isolation",
      mockUpdates: "Updates",
      mockUpdatesSub: "Checked via GitHub Releases",
      whyEyebrow: "Why Guven-ON",
      whyTitle: "Protection without the babysitting.",
      whySub:
        "Most privacy tools ask you to choose between safety and a working laptop. Guven-ON is built around a different idea: the safe path should be the quiet one.",
      why1Title: "DNS leaks, closed.",
      why1Body:
        "When you enable protection on untrusted Wi-Fi, Guven-ON points the system at Cloudflare and Quad9, permits DNS only to those resolvers, and blocks plain DNS and DNS-over-TLS to everything else. No silent leaks to the café router.",
      why2Title: "Local exposure, contained.",
      why2Body:
        "Protection isolates private IPv4 ranges, multicast and legacy discovery. A printer or captive portal can be reached through the ten-minute pause; a configurable LAN allowlist is on the roadmap.",
      why3Title: "Quiet by design.",
      why3Body:
        "Protection is off until you turn it on, and the guards live in the platform filter engine. No tunnel crypto, no keepalive, no packet capture, no per-query logs.",
      howEyebrow: "How it works",
      howTitle: "One click. Then forget it.",
      howSub:
        "Guven-ON lives in the notification area and steps in only when the network can't be trusted.",
      step1Title: "Join a network",
      step1Body: "Join the untrusted Wi-Fi, then turn on protection from the tray. Home networks stay untouched until you choose to enable it.",
      step2Title: "Protection kicks in",
      step2Body:
        "Trusted DNS, the DNS guard and local-network isolation are applied before the next query leaves your laptop.",
      step3Title: "You stay in control",
      step3Body:
        "Use the ten-minute pause for a captive portal or a printer. Panic turns everything off and restores DNS immediately.",
      privacyEyebrow: "Privacy by design",
      privacyTitle: "No account. No profiling. No DNS logs.",
      privacyBody:
        "Guven-ON is a local security tool, not a data business. The service is designed to emit counters and state changes — never the sites you visit.",
      privacy1: "Per-query DNS logging is off by design.",
      privacy2: "Settings stay on your device.",
      privacy3: "Open update manifest and published SHA-256 for every release.",
      langEyebrow: "Languages",
      langTitle: "Speaks your language.",
      langBody:
        "The interface is available in English, German and Turkish. The service, logs and command line stay in English so support is predictable.",
      faqEyebrow: "Questions",
      faqTitle: "Good to know.",
      faq0Q: "What does the preview enforce?",
      faq0A:
        "On Windows, enabling protection applies persistent WFP filters and points active adapters at Cloudflare and Quad9: DNS and DoT to other resolvers, private LAN ranges, multicast and legacy discovery are blocked. The filters survive a service crash, and a self-test verifies them. On Linux the same policy is compiled to nftables. This is alpha: DoH on port 443, some IPv6 traffic, VPN split tunneling, local printers and automatic trust profiles are not covered yet. Protection is off until you enable it.",
      faq1Q: "Does it slow down my connection?",
      faq1A:
        "Protection uses platform packet filters and plain DNS to Cloudflare or Quad9. There is no tunnel encryption, no keepalive and no packet capture, so the overhead is close to the DNS lookup you were already making.",
      faq2Q: "Will it break my printer, NAS or Chromecast?",
      faq2A:
        "While protection is on, private LAN ranges and legacy discovery are isolated, so a printer or casting device may be unreachable. Use the ten-minute pause for local devices or a captive portal. A LAN allowlist is planned.",
      faq3Q: "Is Guven-ON a VPN?",
      faq3A:
        "No. It is a local leak blocker built on the Windows Filtering Platform and nftables on Linux. It does not route your traffic through a remote server, and it does not need an account.",
      faq4Q: "Do you log my DNS queries?",
      faq4A:
        "No. The service keeps aggregate state and counters for the UI. Query names are not written to disk.",
      faq5Q: "Which platforms are supported?",
      faq5A:
        "Windows 11 22H2 or later, x64, and a Linux alpha .deb for x64 using systemd-resolved and nftables. Windows arm64 and Linux arm64 packages are planned.",
      faq6Q: "Why might SmartScreen warn me?",
      faq6A:
        "Preview builds are not code-signed yet. Verify the SHA-256 published with each release, or build from source. Signing and reputation work are part of the release checklist.",
      ctaTitle: "Take your privacy back.",
      ctaBody:
        "Install once. Guven-ON starts with Windows or the Linux service, stays out of the way, and is built for the networks you don't control.",
      ctaDownload: "Download Guven-ON",
      ctaMicro: "Windows 11 22H2+ · x64 · Linux .deb alpha · Preview build",
      downloadNote:
        "Preview builds are unsigned until code signing is set up. Check the release notes and SHA-256 before running.",
      linuxNote:
        "Linux alpha: install the .deb from the same release, then run sudo guvenonctl enable.",
      footerTagline: "Quiet protection for untrusted networks.",
      footerNote: "Not affiliated with Microsoft.",
      footerProduct: "Product",
      footerResources: "Resources",
      footerReleases: "Releases",
      footerIssues: "Issues",
      footerPrivacy: "Privacy",
      footerCopyright: "© 2026 Guven-ON. Built independently."
    },

    de: {
      metaTitle: "Guven-ON — Datenschutz, der einfach funktioniert",
      metaDescription:
        "Guven-ON bietet opt-in DNS- und Lokalnetz-Schutz für unsichere WLANs unter Windows und Linux – leise im Hintergrund. Englisch, Deutsch und Türkisch.",
      skip: "Zum Inhalt springen",
      navWhy: "Warum",
      navHow: "So funktioniert's",
      navPrivacy: "Datenschutz",
      navFaq: "FAQ",
      navDownload: "Herunterladen",
      heroBadge: "Windows 11 · Linux-Alpha · Vorschau",
      heroTitle1: "Datenschutz,",
      heroTitle2: "einfach gemacht.",
      heroSub:
        "Guven-ON soll DNS-Leaks und lokale Netzwerkexposition in unsicheren WLANs blockieren – leise im Hintergrund. Kein Konto. Kein Tunnel. Kein ständiges Nachsehen.",
      heroDownload: "Für Windows herunterladen",
      heroHow: "So funktioniert es",
      heroMicro: "Vorschau-Build · opt-in Schutz · Windows 11 22H2+ · Linux-.deb-Alpha",
      mockPreview: "Vorschau",
      mockStatus: "Schutz bereit",
      mockStatusSub: "Dienst, Tray und opt-in Durchsetzung installiert",
      mockNetwork: "WLAN",
      mockNetworkSub: "Cafe_Gast",
      mockDns: "DNS-Richtlinie",
      mockLan: "LAN-Isolation",
      mockUpdates: "Updates",
      mockUpdatesSub: "Geprüft über GitHub Releases",
      whyEyebrow: "Warum Guven-ON",
      whyTitle: "Schutz ohne ständige Aufsicht.",
      whySub:
        "Die meisten Datenschutztools verlangen eine Wahl zwischen Sicherheit und einem funktionierenden Laptop. Guven-ON folgt einer anderen Idee: Der sichere Weg soll der leise sein.",
      why1Title: "DNS-Leaks: geschlossen.",
      why1Body:
        "Wenn Sie den Schutz in unsicheren WLANs aktivieren, setzt Guven-ON das System auf Cloudflare und Quad9, erlaubt DNS nur zu diesen Resolvern und blockiert normales DNS sowie DNS-over-TLS zu allen anderen. Keine stillen Leaks an den Router im Café.",
      why2Title: "Lokale Exposition begrenzt.",
      why2Body:
        "Der Schutz isoliert private IPv4-Bereiche, Multicast und ältere Discovery-Protokolle. Drucker oder Captive Portals erreichen Sie über die zehnminütige Pause; eine konfigurierbare LAN-Allowlist ist geplant.",
      why3Title: "Leise durch Design.",
      why3Body:
        "Der Schutz ist aus, bis Sie ihn einschalten, und die Regeln laufen in der Filterplattform des Systems. Keine Tunnel-Kryptografie, kein Keepalive, keine Paketaufzeichnung, keine Logs pro Abfrage.",
      howEyebrow: "So funktioniert es",
      howTitle: "Ein Klick. Dann vergessen.",
      howSub:
        "Guven-ON lebt im Infobereich und greift nur ein, wenn dem Netzwerk nicht zu trauen ist.",
      step1Title: "Netzwerk beitreten",
      step1Body: "Verbinden Sie sich mit dem unsicheren WLAN und aktivieren Sie den Schutz im Tray. Heimnetze bleiben unangetastet, bis Sie ihn einschalten.",
      step2Title: "Schutz greift",
      step2Body:
        "Vertrauenswürdiges DNS, die DNS-Sperre und die Lokalnetz-Isolation werden angewendet, bevor die nächste Abfrage den Laptop verlässt.",
      step3Title: "Sie behalten die Kontrolle",
      step3Body:
        "Nutzen Sie die zehnminütige Pause für ein Captive Portal oder einen Drucker. Panik schaltet alles aus und stellt DNS sofort wieder her.",
      privacyEyebrow: "Datenschutz durch Design",
      privacyTitle: "Kein Konto. Kein Profiling. Keine DNS-Logs.",
      privacyBody:
        "Guven-ON ist ein lokales Sicherheitswerkzeug, kein Datenunternehmen. Der Dienst soll Zähler und Statusänderungen melden – niemals die besuchten Seiten.",
      privacy1: "DNS-Logging pro Abfrage ist konstruktiv aus.",
      privacy2: "Einstellungen bleiben auf Ihrem Gerät.",
      privacy3: "Offenes Update-Manifest und veröffentlichte SHA-256 für jedes Release.",
      langEyebrow: "Sprachen",
      langTitle: "Spricht Ihre Sprache.",
      langBody:
        "Die Oberfläche ist auf Englisch, Deutsch und Türkisch verfügbar. Dienst, Logs und Kommandozeile bleiben auf Englisch, damit der Support berechenbar ist.",
      faqEyebrow: "Fragen",
      faqTitle: "Gut zu wissen.",
      faq0Q: "Was setzt die Vorschau durch?",
      faq0A:
        "Unter Windows wendet der aktivierte Schutz persistente WFP-Filter an und richtet aktive Adapter auf Cloudflare und Quad9: DNS und DoT zu anderen Resolvern, private LAN-Bereiche, Multicast und ältere Discovery-Protokolle werden blockiert. Die Filter überstehen einen Dienstabsturz und werden durch einen Selbsttest geprüft. Unter Linux wird dieselbe Richtlinie nach nftables übersetzt. Das ist Alpha: DoH auf Port 443, einige IPv6-Verbindungen, VPN-Split-Tunneling, lokale Drucker und automatische Vertrauensprofile sind noch nicht abgedeckt. Der Schutz ist aus, bis Sie ihn aktivieren.",
      faq1Q: "Wird meine Verbindung langsamer?",
      faq1A:
        "Der Schutz nutzt Paketfilter der Plattform und normales DNS zu Cloudflare oder Quad9. Es gibt keine Tunnelverschlüsselung, kein Keepalive und keine Paketaufzeichnung – der Aufwand liegt nahe am DNS-Lookup, den Sie ohnehin machen.",
      faq2Q: "Bricht das meinen Drucker, NAS oder Chromecast?",
      faq2A:
        "Während der Schutz aktiv ist, werden private LAN-Bereiche und ältere Discovery-Protokolle isoliert; ein Drucker oder Casting-Gerät kann dann unerreichbar sein. Nutzen Sie die zehnminütige Pause für lokale Geräte oder ein Captive Portal. Eine LAN-Allowlist ist geplant.",
      faq3Q: "Ist Guven-ON ein VPN?",
      faq3A:
        "Nein. Es ist ein lokaler Leak-Blocker auf Basis der Windows-Filterplattform und von nftables unter Linux. Der Verkehr wird nicht über einen entfernten Server geleitet, und es ist kein Konto nötig.",
      faq4Q: "Protokollieren Sie meine DNS-Abfragen?",
      faq4A:
        "Nein. Der Dienst speichert aggregierte Zustände und Zähler für die Oberfläche. Abfragenamen werden nicht auf die Festplatte geschrieben.",
      faq5Q: "Welche Plattformen werden unterstützt?",
      faq5A:
        "Windows 11 22H2 oder neuer, x64, sowie eine Linux-Alpha als .deb für x64 mit systemd-resolved und nftables. Windows-arm64- und Linux-arm64-Pakete sind geplant.",
      faq6Q: "Warum warnt SmartScreen möglicherweise?",
      faq6A:
        "Vorschau-Builds sind noch nicht codesigniert. Prüfen Sie die mit jedem Release veröffentlichte SHA-256 oder bauen Sie aus dem Quellcode. Signierung und Reputation sind Teil der Release-Checkliste.",
      ctaTitle: "Holen Sie sich Ihre Privatsphäre zurück.",
      ctaBody:
        "Einmal installieren. Guven-ON startet mit Windows oder dem Linux-Dienst, bleibt im Hintergrund und ist für die Netzwerke gebaut, die Sie nicht kontrollieren.",
      ctaDownload: "Guven-ON herunterladen",
      ctaMicro: "Windows 11 22H2+ · x64 · Linux-.deb-Alpha · Vorschau-Build",
      downloadNote:
        "Vorschau-Builds sind bis zur Einrichtung der Codesignatur unsigniert. Prüfen Sie Release-Notizen und SHA-256 vor dem Start.",
      linuxNote:
        "Linux-Alpha: Installieren Sie die .deb aus demselben Release und führen Sie danach sudo guvenonctl enable aus.",
      footerTagline: "Leiser Schutz für unsichere Netzwerke.",
      footerNote: "Nicht mit Microsoft verbunden.",
      footerProduct: "Produkt",
      footerResources: "Ressourcen",
      footerReleases: "Releases",
      footerIssues: "Probleme",
      footerPrivacy: "Datenschutz",
      footerCopyright: "© 2026 Guven-ON. Unabhängig entwickelt."
    },

    tr: {
      metaTitle: "Guven-ON — zahmetsiz gizlilik",
      metaDescription:
        "Guven-ON, Windows ve Linux'ta güvenilmeyen Wi-Fi ağlarında isteğe bağlı DNS ve yerel ağ koruması sunar; arka planda sessizce çalışır. İngilizce, Almanca ve Türkçe.",
      skip: "İçeriğe geç",
      navWhy: "Neden",
      navHow: "Nasıl çalışır",
      navPrivacy: "Gizlilik",
      navFaq: "SSS",
      navDownload: "İndir",
      heroBadge: "Windows 11 · Linux alfa · Önizleme",
      heroTitle1: "Zahmetsiz",
      heroTitle2: "gizlilik.",
      heroSub:
        "Guven-ON güvenilmeyen Wi-Fi ağlarında DNS sızıntılarını ve yerel ağ maruziyetini engellemek için tasarlandı — arka planda sessizce. Hesap yok. Tünel yok. Sürekli ilgilenmeniz gerekmez.",
      heroDownload: "Windows için indir",
      heroHow: "Nasıl çalıştığını gör",
      heroMicro: "Önizleme sürümü · isteğe bağlı koruma · Windows 11 22H2+ · Linux .deb alfa",
      mockPreview: "Önizleme",
      mockStatus: "Koruma hazır",
      mockStatusSub: "Servis, tepsi ve isteğe bağlı uygulama kurulu",
      mockNetwork: "Wi-Fi",
      mockNetworkSub: "Kafe_Misafir",
      mockDns: "DNS ilkesi",
      mockLan: "LAN izolasyonu",
      mockUpdates: "Güncellemeler",
      mockUpdatesSub: "GitHub Releases üzerinden denetlenir",
      whyEyebrow: "Neden Guven-ON",
      whyTitle: "Sürekli ilgi istemeyen koruma.",
      whySub:
        "Çoğu gizlilik aracı sizi güvenlik ile çalışan bir dizüstü bilgisayar arasında seçim yapmaya zorlar. Guven-ON farklı bir fikre dayanır: güvenli yol, sessiz yol olmalıdır.",
      why1Title: "DNS sızıntıları kapandı.",
      why1Body:
        "Güvenilmeyen Wi-Fi'de korumayı açtığınızda Guven-ON sistemi Cloudflare ve Quad9'a yönlendirir, DNS'e yalnızca bu çözümleyicilere izin verir, diğer tüm düz DNS ve DNS-over-TLS sorgularını engeller. Kafedeki yönlendiriciye sessiz sızıntı yok.",
      why2Title: "Yerel maruziyet sınırlandı.",
      why2Body:
        "Koruma özel IPv4 aralıklarını, çoklu yayını ve eski keşif protokollerini izole eder. Yazıcıya veya captive portal'a on dakikalık duraklama ile erişebilirsiniz; yapılandırılabilir LAN izin listesi yol haritasında.",
      why3Title: "Tasarımı gereği sessiz.",
      why3Body:
        "Siz açana kadar koruma kapalıdır ve kurallar platformun filtre motorunda çalışır. Tünel şifrelemesi, keepalive, paket yakalama veya sorgu başına günlük yok.",
      howEyebrow: "Nasıl çalışır",
      howTitle: "Tek tık. Sonra unutun.",
      howSub:
        "Guven-ON sistem tepsisinde yaşar ve yalnızca ağa güvenilemeyeceği zaman devreye girer.",
      step1Title: "Ağa katılın",
      step1Body: "Güvenilmeyen Wi-Fi'ye bağlanın ve korumayı tepside açın. Ev ağları siz açana kadar dokunulmadan kalır.",
      step2Title: "Koruma devreye girer",
      step2Body:
        "Güvenilir DNS, DNS koruması ve yerel ağ izolasyonu bir sonraki sorgu dizüstü bilgisayarınızdan çıkmadan uygulanır.",
      step3Title: "Kontrolde kalın",
      step3Body:
        "Captive portal veya yazıcı için on dakikalık duraklamayı kullanın. Panik her şeyi kapatır ve DNS'i hemen geri yükler.",
      privacyEyebrow: "Tasarımı gereği gizlilik",
      privacyTitle: "Hesap yok. Profil çıkarma yok. DNS günlüğü yok.",
      privacyBody:
        "Guven-ON yerel bir güvenlik aracıdır, bir veri işletmesi değildir. Servis yalnızca sayaçlar ve durum değişiklikleri üretmek üzere tasarlandı — ziyaret ettiğiniz siteleri asla üretmez.",
      privacy1: "Sorgu başına DNS günlüğü tasarım gereği kapalıdır.",
      privacy2: "Ayarlar cihazınızda kalır.",
      privacy3: "Açık güncelleme bildirimi ve her sürüm için yayımlanmış SHA-256.",
      langEyebrow: "Diller",
      langTitle: "Sizin dilinizi konuşur.",
      langBody:
        "Arayüz İngilizce, Almanca ve Türkçe olarak kullanılabilir. Servis, günlükler ve komut satırı İngilizce kalır; böylece destek öngörülebilir olur.",
      faqEyebrow: "Sorular",
      faqTitle: "Bilmeniz iyi olur.",
      faq0Q: "Önizleme neyi uyguluyor?",
      faq0A:
        "Windows'ta koruma açıldığında kalıcı WFP filtreleri uygulanır ve etkin bağdaştırıcılar Cloudflare ile Quad9'a yönlendirilir: diğer çözümleyicilere DNS ve DoT, özel LAN aralıkları, çoklu yayın ve eski keşif protokolleri engellenir. Filtreler servis çökmesinden sonra korunur ve bir öz test ile doğrulanır. Linux'ta aynı ilke nftables'a derlenir. Bu bir alfa: 443 portundaki DoH, bazı IPv6 trafiği, VPN bölünmüş tünelleme, yerel yazıcılar ve otomatik güven profilleri henüz kapsam dışıdır. Koruma siz açana kadar kapalıdır.",
      faq1Q: "Bağlantımı yavaşlatır mı?",
      faq1A:
        "Koruma platform paket filtrelerini ve Cloudflare veya Quad9'a düz DNS kullanır. Tünel şifrelemesi, keepalive ve paket yakalama yoktur; ek yük zaten yaptığınız DNS sorgusuna yakındır.",
      faq2Q: "Yazıcımı, NAS'ımı veya Chromecast'imi bozar mı?",
      faq2A:
        "Koruma açıkken özel LAN aralıkları ve eski keşif protokolleri izole edilir; yazıcı veya yayın cihazı erişilemez olabilir. Yerel cihazlar veya captive portal için on dakikalık duraklamayı kullanın. LAN izin listesi planlanıyor.",
      faq3Q: "Guven-ON bir VPN mi?",
      faq3A:
        "Hayır. Windows Filtering Platform ve Linux'ta nftables üzerinde çalışan yerel bir sızıntı engelleyicidir. Trafiğinizi uzak bir sunucuya yönlendirmez ve hesap gerektirmez.",
      faq4Q: "DNS sorgularımı günlüğe kaydediyor musunuz?",
      faq4A:
        "Hayır. Servis, arayüz için toplu durum ve sayaçlar tutar. Sorgu adları diske yazılmaz.",
      faq5Q: "Hangi platformlar destekleniyor?",
      faq5A:
        "Windows 11 22H2 veya üzeri, x64 ve systemd-resolved ile nftables kullanan x64 Linux alfa .deb paketi. Windows arm64 ve Linux arm64 paketleri planlanıyor.",
      faq6Q: "SmartScreen neden uyarabilir?",
      faq6A:
        "Önizleme sürümleri henüz kod imzalı değil. Her sürümle yayımlanan SHA-256'yı doğrulayın veya kaynaktan derleyin. İmzalama ve itibar çalışmaları sürüm kontrol listesinin parçasıdır.",
      ctaTitle: "Gizliliğinizi geri alın.",
      ctaBody:
        "Bir kez kurun. Guven-ON Windows veya Linux servisiyle başlar, yoldan çekilir ve kontrol etmediğiniz ağlar için tasarlanmıştır.",
      ctaDownload: "Guven-ON'u indir",
      ctaMicro: "Windows 11 22H2+ · x64 · Linux .deb alfa · Önizleme sürümü",
      downloadNote:
        "Kod imzalama kurulana kadar önizleme sürümleri imzasızdır. Çalıştırmadan önce sürüm notlarını ve SHA-256'yı kontrol edin.",
      linuxNote:
        "Linux alfa: Aynı sürümdeki .deb paketini kurun, ardından sudo guvenonctl enable çalıştırın.",
      footerTagline: "Güvenilmeyen ağlar için sessiz koruma.",
      footerNote: "Microsoft ile bağlantılı değildir.",
      footerProduct: "Ürün",
      footerResources: "Kaynaklar",
      footerReleases: "Sürümler",
      footerIssues: "Sorunlar",
      footerPrivacy: "Gizlilik",
      footerCopyright: "© 2026 Guven-ON. Bağımsız olarak geliştirildi."
    }
  };

  const root = document.documentElement;
  const description = document.querySelector('meta[name="description"]');
  const ogTitle = document.querySelector('meta[property="og:title"]');
  const ogDescription = document.querySelector('meta[property="og:description"]');
  const twitterTitle = document.querySelector('meta[name="twitter:title"]');
  const twitterDescription = document.querySelector('meta[name="twitter:description"]');
  const languageButtons = Array.from(document.querySelectorAll("[data-set-lang]"));
  let currentSelection = "auto";

  function normalize(value) {
    const code = String(value || "").trim().slice(0, 2).toLowerCase();
    return SUPPORTED.includes(code) ? code : null;
  }

  function browserLanguage() {
    const candidates = [];

    if (Array.isArray(navigator.languages)) {
      candidates.push(...navigator.languages);
    }
    if (navigator.language) {
      candidates.push(navigator.language);
    }
    try {
      candidates.push(new Intl.DateTimeFormat().resolvedOptions().locale);
    } catch {
      // Intl may be unavailable in very old embedded browsers.
    }
    candidates.push(root.lang);

    for (const candidate of candidates) {
      const code = normalize(candidate);
      if (code) {
        return code;
      }
    }

    return "en";
  }

  function detectSelection() {
    const query = new URLSearchParams(window.location.search).get("lang");
    if (String(query || "").toLowerCase() === "auto") {
      return "auto";
    }

    const queryCode = normalize(query);
    if (queryCode) {
      return queryCode;
    }

    try {
      const stored = String(window.localStorage.getItem(STORAGE_KEY) || "").toLowerCase();
      if (stored === "auto") {
        return "auto";
      }

      const storedCode = normalize(stored);
      if (storedCode) {
        return storedCode;
      }
    } catch {
      // Storage may be disabled; fall through to browser/system detection.
    }

    return "auto";
  }

  function applyLanguage(selection, persist) {
    const auto = String(selection || "auto").toLowerCase() === "auto";
    const active = auto ? browserLanguage() : normalize(selection) || browserLanguage();
    const dictionary = I18N[active] || I18N.en;

    currentSelection = auto ? "auto" : active;
    root.lang = active;
    root.dataset.lang = active;

    document.querySelectorAll("[data-i18n]").forEach((element) => {
      const key = element.dataset.i18n;
      const value = dictionary[key] || I18N.en[key];
      if (typeof value === "string") {
        element.textContent = value;
      }
    });

    document.title = dictionary.metaTitle || I18N.en.metaTitle;

    if (description) {
      description.setAttribute("content", dictionary.metaDescription || I18N.en.metaDescription);
    }
    if (ogTitle) {
      ogTitle.setAttribute("content", dictionary.metaTitle || I18N.en.metaTitle);
    }
    if (ogDescription) {
      ogDescription.setAttribute("content", dictionary.metaDescription || I18N.en.metaDescription);
    }
    if (twitterTitle) {
      twitterTitle.setAttribute("content", dictionary.metaTitle || I18N.en.metaTitle);
    }
    if (twitterDescription) {
      twitterDescription.setAttribute("content", dictionary.metaDescription || I18N.en.metaDescription);
    }

    languageButtons.forEach((button) => {
      button.setAttribute("aria-pressed", String(button.dataset.setLang === currentSelection));
    });

    if (persist) {
      try {
        window.localStorage.setItem(STORAGE_KEY, currentSelection);
      } catch {
        // Best effort only.
      }

      try {
        const url = new URL(window.location.href);
        if (currentSelection === "auto") {
          url.searchParams.delete("lang");
        } else {
          url.searchParams.set("lang", currentSelection);
        }
        window.history.replaceState(null, "", url.toString());
      } catch {
        // History may be restricted in embedded browsers.
      }
    }
  }

  function switchLanguage(language) {
    const requested = String(language || "auto").toLowerCase();
    const next = requested === "auto" ? "auto" : normalize(requested);
    if (!next || next === currentSelection) {
      return;
    }

    document.body.classList.add("lang-switching");
    window.setTimeout(() => {
      applyLanguage(next, true);
      document.body.classList.remove("lang-switching");
    }, 140);
  }

  languageButtons.forEach((button) => {
    button.addEventListener("click", () => switchLanguage(button.dataset.setLang));
  });

  // If the visitor chose automatic mode, follow a browser/system language change.
  window.addEventListener("languagechange", () => {
    if (currentSelection === "auto") {
      applyLanguage("auto", false);
    }
  });

  // Download and repository links stay in one place.
  document.querySelectorAll("[data-download]").forEach((link) => {
    link.href = `${REPO_URL}/releases/latest`;
    link.target = "_blank";
    link.rel = "noopener";
  });

  document.querySelectorAll("[data-repo-link]").forEach((link) => {
    const kind = link.dataset.repoLink;
    link.href =
      kind === "releases"
        ? `${REPO_URL}/releases`
        : kind === "issues"
          ? `${REPO_URL}/issues`
          : REPO_URL;
    link.target = "_blank";
    link.rel = "noopener";
  });

  // Reveal sections as they enter the viewport.
  const revealItems = document.querySelectorAll(".reveal");
  if ("IntersectionObserver" in window) {
    const observer = new IntersectionObserver(
      (entries) => {
        entries.forEach((entry) => {
          if (entry.isIntersecting) {
            entry.target.classList.add("in");
            observer.unobserve(entry.target);
          }
        });
      },
      { threshold: 0.14, rootMargin: "0px 0px -6% 0px" }
    );

    revealItems.forEach((item) => {
      const rect = item.getBoundingClientRect();
      if (rect.top < window.innerHeight && rect.bottom > 0) {
        item.classList.add("in");
      } else {
        observer.observe(item);
      }
    });
  } else {
    revealItems.forEach((item) => item.classList.add("in"));
  }

  // Navigation elevation on scroll.
  const nav = document.getElementById("siteNav");
  if (nav) {
    const updateNav = () => nav.classList.toggle("scrolled", window.scrollY > 10);
    updateNav();
    window.addEventListener("scroll", updateNav, { passive: true });
  }

  // Cursor-aware hero: gentle card tilt and a moving light source.
  const hero = document.querySelector(".hero");
  const appWindow = document.getElementById("appWindow");
  const finePointer = window.matchMedia("(pointer: fine)").matches;
  const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;

  if (hero && appWindow && finePointer && !reducedMotion) {
    let frame = 0;
    let targetX = 0;
    let targetY = 0;

    const updateTilt = () => {
      frame = 0;
      appWindow.style.setProperty("--ry", `${(targetX * 4).toFixed(2)}deg`);
      appWindow.style.setProperty("--rx", `${(-targetY * 3).toFixed(2)}deg`);
      appWindow.style.setProperty("--ty", `${(targetY * 5).toFixed(2)}px`);
    };

    hero.addEventListener("pointermove", (event) => {
      const rect = hero.getBoundingClientRect();
      const x = (event.clientX - rect.left) / rect.width;
      const y = (event.clientY - rect.top) / rect.height;

      hero.style.setProperty("--mx", `${(x * 100).toFixed(1)}%`);
      hero.style.setProperty("--my", `${(y * 100).toFixed(1)}%`);

      targetX = (x - 0.5) * 2;
      targetY = (y - 0.5) * 2;

      if (!frame) {
        frame = window.requestAnimationFrame(updateTilt);
      }
    });

    hero.addEventListener("pointerleave", () => {
      appWindow.style.setProperty("--ry", "0deg");
      appWindow.style.setProperty("--rx", "0deg");
      appWindow.style.setProperty("--ty", "0px");
      hero.style.setProperty("--mx", "72%");
      hero.style.setProperty("--my", "24%");
    });
  }

  // Keep the FAQ tidy: one open answer at a time.
  const faqItems = Array.from(document.querySelectorAll(".faq-item"));
  faqItems.forEach((item) => {
    item.addEventListener("toggle", () => {
      if (!item.open) {
        return;
      }
      faqItems.forEach((other) => {
        if (other !== item) {
          other.open = false;
        }
      });
    });
  });

  applyLanguage(detectSelection(), false);
})();
