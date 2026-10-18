(() => {
  "use strict";

  const REPO = "guvenon/guvenon";
  const REPO_URL = `https://github.com/${REPO}`;
  const SUPPORTED = ["en", "de", "tr"];
  const STORAGE_KEY = "guvenon.lang";

  const I18N = {
    en: {
      metaTitle: "Guven-ON — Privacy that just works on Windows",
      metaDescription:
        "Guven-ON is designed to block DNS leaks and local-network exposure on untrusted Wi-Fi, stays quiet in the background, and speaks English, German and Turkish.",
      skip: "Skip to content",
      navWhy: "Why",
      navHow: "How it works",
      navPrivacy: "Privacy",
      navFaq: "FAQ",
      navDownload: "Download",
      heroBadge: "Windows 11 · Preview",
      heroTitle1: "Privacy that",
      heroTitle2: "just works.",
      heroSub:
        "Guven-ON is built to block DNS leaks and local-network exposure on untrusted Wi-Fi — quietly, in the background. No account. No tunnel. No babysitting.",
      heroDownload: "Download for Windows",
      heroHow: "See how it works",
      heroMicro: "Preview build · filtering not enabled yet · Windows 11 22H2+ · x64",
      mockPreview: "Preview",
      mockStatus: "Preview ready",
      mockStatusSub: "Service and tray installed · filtering comes next",
      mockNetwork: "Wi-Fi",
      mockNetworkSub: "CoffeeShop_Guest",
      mockDns: "DNS policy",
      mockLan: "LAN allowlist",
      mockUpdates: "Updates",
      mockUpdatesSub: "Checked via GitHub Releases",
      whyEyebrow: "Why Guven-ON",
      whyTitle: "Protection without the babysitting.",
      whySub:
        "Most privacy tools ask you to choose between safety and a working laptop. Guven-ON is built around a different idea: the safe path should be the quiet one.",
      why1Title: "DNS leaks, closed.",
      why1Body:
        "When you join untrusted Wi-Fi, Guven-ON is designed to keep DNS on the encrypted path and block queries that try to escape. No silent leaks to the café router.",
      why2Title: "Your printer still works.",
      why2Body:
        "Local devices are not the enemy. A clear LAN allowlist is designed to keep printers, NAS boxes and casting working while untrusted peers stay out.",
      why3Title: "Quiet by design.",
      why3Body:
        "DNS-only mode by default. No tunnel crypto, no keepalive, no packet capture, no per-query logs. Enforcement will live in the Windows filter engine, where it costs almost nothing.",
      howEyebrow: "How it works",
      howTitle: "One click. Then forget it.",
      howSub:
        "Guven-ON lives in the notification area and steps in only when the network can't be trusted.",
      step1Title: "Join a network",
      step1Body: "Guven-ON notices the profile. Home and work networks stay untouched.",
      step2Title: "Protection kicks in",
      step2Body:
        "In the enforcement build, DNS and local traffic are pinned down before the first query leaves your laptop.",
      step3Title: "You keep working",
      step3Body:
        "Captive portals, printers and video calls keep working. A panic reset is always one click away.",
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
      faq0Q: "Does the preview protect me yet?",
      faq0A:
        "No. The preview installs the service and tray UI, but it does not filter DNS or local traffic yet. Do not rely on it as a security control. Enforcement is the next milestone.",
      faq1Q: "Does it slow down my connection?",
      faq1A:
        "By default Guven-ON runs in DNS-only mode. There is no tunnel encryption, no keepalive and no packet capture, so the overhead is effectively the cost of the DNS lookup you were already making.",
      faq2Q: "Will it break my printer, NAS or Chromecast?",
      faq2A:
        "No. Local-network access is controlled by an allowlist, not switched off blindly. Captive portals are detected and handled before enforcement kicks in.",
      faq3Q: "Is Guven-ON a VPN?",
      faq3A:
        "No. It is a leak blocker that works with the Windows filtering platform. It does not route your traffic through a remote server, and it does not need an account.",
      faq4Q: "Do you log my DNS queries?",
      faq4A:
        "No. The service keeps aggregate state and counters for the UI. Query names are not written to disk.",
      faq5Q: "Which Windows versions are supported?",
      faq5A:
        "Windows 11 22H2 or later, x64, is the first target. Windows 10 support is planned after the Windows 11 release.",
      faq6Q: "Why might SmartScreen warn me?",
      faq6A:
        "Preview builds are not code-signed yet. Verify the SHA-256 published with each release, or build from source. Signing and reputation work are part of the release checklist.",
      ctaTitle: "Take your privacy back.",
      ctaBody:
        "Install once. Guven-ON starts with Windows, stays out of the way, and is built for the networks you don't control.",
      ctaDownload: "Download Guven-ON",
      ctaMicro: "Windows 11 22H2+ · x64 · Preview build",
      downloadNote:
        "Preview builds are unsigned until code signing is set up. Check the release notes and SHA-256 before running.",
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
        "Guven-ON soll DNS-Leaks und lokale Netzwerkexposition in unsicheren WLANs blockieren – leise im Hintergrund. Englisch, Deutsch und Türkisch.",
      skip: "Zum Inhalt springen",
      navWhy: "Warum",
      navHow: "So funktioniert's",
      navPrivacy: "Datenschutz",
      navFaq: "FAQ",
      navDownload: "Herunterladen",
      heroBadge: "Windows 11 · Vorschau",
      heroTitle1: "Datenschutz,",
      heroTitle2: "einfach gemacht.",
      heroSub:
        "Guven-ON soll DNS-Leaks und lokale Netzwerkexposition in unsicheren WLANs blockieren – leise im Hintergrund. Kein Konto. Kein Tunnel. Kein ständiges Nachsehen.",
      heroDownload: "Für Windows herunterladen",
      heroHow: "So funktioniert es",
      heroMicro: "Vorschau-Build · Filterung noch nicht aktiv · Windows 11 22H2+ · x64",
      mockPreview: "Vorschau",
      mockStatus: "Vorschau bereit",
      mockStatusSub: "Dienst und Tray installiert · Filterung folgt",
      mockNetwork: "WLAN",
      mockNetworkSub: "Cafe_Gast",
      mockDns: "DNS-Richtlinie",
      mockLan: "LAN-Allowlist",
      mockUpdates: "Updates",
      mockUpdatesSub: "Geprüft über GitHub Releases",
      whyEyebrow: "Warum Guven-ON",
      whyTitle: "Schutz ohne ständige Aufsicht.",
      whySub:
        "Die meisten Datenschutztools verlangen eine Wahl zwischen Sicherheit und einem funktionierenden Laptop. Guven-ON folgt einer anderen Idee: Der sichere Weg soll der leise sein.",
      why1Title: "DNS-Leaks: geschlossen.",
      why1Body:
        "In unsicheren WLANs soll DNS auf dem verschlüsselten Weg bleiben; Abfragen, die entweichen wollen, sollen blockiert werden. Keine stillen Leaks an den Router im Café.",
      why2Title: "Der Drucker läuft weiter.",
      why2Body:
        "Lokale Geräte sind nicht der Feind. Eine klare LAN-Allowlist soll Drucker, NAS und Casting am Laufen halten, während unsichere Teilnehmer draußen bleiben.",
      why3Title: "Leise durch Design.",
      why3Body:
        "Standardmäßig DNS-only. Keine Tunnel-Kryptografie, kein Keepalive, keine Paketaufzeichnung, keine Logs pro Abfrage. Die Durchsetzung wird in der Windows-Filterplattform laufen und fast nichts kosten.",
      howEyebrow: "So funktioniert es",
      howTitle: "Ein Klick. Dann vergessen.",
      howSub:
        "Guven-ON lebt im Infobereich und greift nur ein, wenn dem Netzwerk nicht zu trauen ist.",
      step1Title: "Netzwerk beitreten",
      step1Body: "Guven-ON erkennt das Profil. Heim- und Arbeitsnetze bleiben unangetastet.",
      step2Title: "Schutz greift",
      step2Body:
        "Im Build mit Durchsetzung werden DNS und lokaler Verkehr festgelegt, bevor die erste Abfrage den Laptop verlässt.",
      step3Title: "Sie arbeiten weiter",
      step3Body:
        "Captive Portals, Drucker und Videoanrufe funktionieren weiter. Ein Panik-Reset ist immer einen Klick entfernt.",
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
      faq0Q: "Schützt mich die Vorschau schon?",
      faq0A:
        "Nein. Die Vorschau installiert Dienst und Tray-Oberfläche, filtert aber noch keinen DNS- oder lokalen Verkehr. Verlassen Sie sich noch nicht darauf. Die Durchsetzung ist der nächste Meilenstein.",
      faq1Q: "Wird meine Verbindung langsamer?",
      faq1A:
        "Standardmäßig läuft Guven-ON im DNS-only-Modus. Es gibt keine Tunnelverschlüsselung, kein Keepalive und keine Paketaufzeichnung – der Aufwand entspricht praktisch dem DNS-Lookup, den Sie ohnehin machen.",
      faq2Q: "Bricht das meinen Drucker, NAS oder Chromecast?",
      faq2A:
        "Nein. Der Zugriff auf das lokale Netz wird über eine Allowlist gesteuert, nicht blind abgeschaltet. Captive Portals werden erkannt und behandelt, bevor die Durchsetzung greift.",
      faq3Q: "Ist Guven-ON ein VPN?",
      faq3A:
        "Nein. Es ist ein Leak-Blocker auf Basis der Windows-Filterplattform. Der Verkehr wird nicht über einen entfernten Server geleitet, und es ist kein Konto nötig.",
      faq4Q: "Protokollieren Sie meine DNS-Abfragen?",
      faq4A:
        "Nein. Der Dienst speichert aggregierte Zustände und Zähler für die Oberfläche. Abfragenamen werden nicht auf die Festplatte geschrieben.",
      faq5Q: "Welche Windows-Versionen werden unterstützt?",
      faq5A:
        "Windows 11 22H2 oder neuer, x64, ist das erste Ziel. Windows 10 folgt nach dem Windows-11-Release.",
      faq6Q: "Warum warnt SmartScreen möglicherweise?",
      faq6A:
        "Vorschau-Builds sind noch nicht codesigniert. Prüfen Sie die mit jedem Release veröffentlichte SHA-256 oder bauen Sie aus dem Quellcode. Signierung und Reputation sind Teil der Release-Checkliste.",
      ctaTitle: "Holen Sie sich Ihre Privatsphäre zurück.",
      ctaBody:
        "Einmal installieren. Guven-ON startet mit Windows, bleibt im Hintergrund und ist für die Netzwerke gebaut, die Sie nicht kontrollieren.",
      ctaDownload: "Guven-ON herunterladen",
      ctaMicro: "Windows 11 22H2+ · x64 · Vorschau-Build",
      downloadNote:
        "Vorschau-Builds sind bis zur Einrichtung der Codesignatur unsigniert. Prüfen Sie Release-Notizen und SHA-256 vor dem Start.",
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
      metaTitle: "Guven-ON — Windows'ta zahmetsiz gizlilik",
      metaDescription:
        "Guven-ON güvenilmeyen Wi-Fi ağlarında DNS sızıntılarını ve yerel ağ maruziyetini engellemek için tasarlandı; arka planda sessizce çalışır. İngilizce, Almanca ve Türkçe.",
      skip: "İçeriğe geç",
      navWhy: "Neden",
      navHow: "Nasıl çalışır",
      navPrivacy: "Gizlilik",
      navFaq: "SSS",
      navDownload: "İndir",
      heroBadge: "Windows 11 · Önizleme",
      heroTitle1: "Zahmetsiz",
      heroTitle2: "gizlilik.",
      heroSub:
        "Guven-ON güvenilmeyen Wi-Fi ağlarında DNS sızıntılarını ve yerel ağ maruziyetini engellemek için tasarlandı — arka planda sessizce. Hesap yok. Tünel yok. Sürekli ilgilenmeniz gerekmez.",
      heroDownload: "Windows için indir",
      heroHow: "Nasıl çalıştığını gör",
      heroMicro: "Önizleme sürümü · filtreleme henüz etkin değil · Windows 11 22H2+ · x64",
      mockPreview: "Önizleme",
      mockStatus: "Önizleme hazır",
      mockStatusSub: "Servis ve tepsi kurulu · filtreleme yakında",
      mockNetwork: "Wi-Fi",
      mockNetworkSub: "Kafe_Misafir",
      mockDns: "DNS ilkesi",
      mockLan: "LAN izin listesi",
      mockUpdates: "Güncellemeler",
      mockUpdatesSub: "GitHub Releases üzerinden denetlenir",
      whyEyebrow: "Neden Guven-ON",
      whyTitle: "Sürekli ilgi istemeyen koruma.",
      whySub:
        "Çoğu gizlilik aracı sizi güvenlik ile çalışan bir dizüstü bilgisayar arasında seçim yapmaya zorlar. Guven-ON farklı bir fikre dayanır: güvenli yol, sessiz yol olmalıdır.",
      why1Title: "DNS sızıntıları kapandı.",
      why1Body:
        "Güvenilmeyen Wi-Fi'ye bağlandığınızda Guven-ON DNS'i şifreli yolda tutmak ve kaçmaya çalışan sorguları engellemek için tasarlandı. Kafedeki yönlendiriciye sessiz sızıntı yok.",
      why2Title: "Yazıcınız hâlâ çalışır.",
      why2Body:
        "Yerel cihazlar düşman değildir. Net bir LAN izin listesi yazıcıları, NAS'ları ve yayınlamayı çalışır tutmak için tasarlandı; güvenilmeyen taraflar dışarıda kalır.",
      why3Title: "Tasarımı gereği sessiz.",
      why3Body:
        "Varsayılan olarak yalnızca DNS modu. Tünel şifrelemesi, keepalive, paket yakalama veya sorgu başına günlük yok. Uygulama Windows filtre platformunda çalışacak ve neredeyse hiç maliyet çıkarmayacak.",
      howEyebrow: "Nasıl çalışır",
      howTitle: "Tek tık. Sonra unutun.",
      howSub:
        "Guven-ON sistem tepsisinde yaşar ve yalnızca ağa güvenilemeyeceği zaman devreye girer.",
      step1Title: "Ağa katılın",
      step1Body: "Guven-ON profili fark eder. Ev ve iş ağlarına dokunmaz.",
      step2Title: "Koruma devreye girer",
      step2Body:
        "Uygulama sürümünde ilk sorgu dizüstü bilgisayarınızdan çıkmadan DNS ve yerel trafik sabitlenir.",
      step3Title: "Çalışmaya devam edin",
      step3Body:
        "Captive portallar, yazıcılar ve görüntülü görüşmeler çalışmaya devam eder. Panik sıfırlama her zaman tek tık uzaklıktadır.",
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
      faq0Q: "Önizleme beni şimdiden koruyor mu?",
      faq0A:
        "Hayır. Önizleme servisi ve tepsi arayüzünü kurar ancak henüz DNS veya yerel trafiği filtrelemez. Güvenlik kontrolü olarak güvenmeyin. Uygulama bir sonraki kilometre taşıdır.",
      faq1Q: "Bağlantımı yavaşlatır mı?",
      faq1A:
        "Guven-ON varsayılan olarak yalnızca DNS modunda çalışır. Tünel şifrelemesi, keepalive ve paket yakalama yoktur; ek yük pratikte zaten yaptığınız DNS sorgusunun maliyetidir.",
      faq2Q: "Yazıcımı, NAS'ımı veya Chromecast'imi bozar mı?",
      faq2A:
        "Hayır. Yerel ağ erişimi körlemesine kapatılmaz; bir izin listesiyle yönetilir. Captive portallar, koruma devreye girmeden önce algılanır ve ele alınır.",
      faq3Q: "Guven-ON bir VPN mi?",
      faq3A:
        "Hayır. Windows filtreleme platformuyla çalışan bir sızıntı engelleyicidir. Trafiğinizi uzak bir sunucuya yönlendirmez ve hesap gerektirmez.",
      faq4Q: "DNS sorgularımı günlüğe kaydediyor musunuz?",
      faq4A:
        "Hayır. Servis, arayüz için toplu durum ve sayaçlar tutar. Sorgu adları diske yazılmaz.",
      faq5Q: "Hangi Windows sürümleri destekleniyor?",
      faq5A:
        "İlk hedef Windows 11 22H2 veya üzeri, x64. Windows 10 desteği Windows 11 sürümünden sonra planlanıyor.",
      faq6Q: "SmartScreen neden uyarabilir?",
      faq6A:
        "Önizleme sürümleri henüz kod imzalı değil. Her sürümle yayımlanan SHA-256'yı doğrulayın veya kaynaktan derleyin. İmzalama ve itibar çalışmaları sürüm kontrol listesinin parçasıdır.",
      ctaTitle: "Gizliliğinizi geri alın.",
      ctaBody:
        "Bir kez kurun. Guven-ON Windows ile başlar, yoldan çekilir ve kontrol etmediğiniz ağlar için tasarlanmıştır.",
      ctaDownload: "Guven-ON'u indir",
      ctaMicro: "Windows 11 22H2+ · x64 · Önizleme sürümü",
      downloadNote:
        "Kod imzalama kurulana kadar önizleme sürümleri imzasızdır. Çalıştırmadan önce sürüm notlarını ve SHA-256'yı kontrol edin.",
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
  const languageButtons = Array.from(document.querySelectorAll("[data-set-lang]"));
  let currentLanguage = "en";

  function normalize(value) {
    const code = String(value || "").slice(0, 2).toLowerCase();
    return SUPPORTED.includes(code) ? code : null;
  }

  function detectLanguage() {
    const query = normalize(new URLSearchParams(window.location.search).get("lang"));
    if (query) {
      return query;
    }

    try {
      const stored = normalize(window.localStorage.getItem(STORAGE_KEY));
      if (stored) {
        return stored;
      }
    } catch {
      // Storage may be disabled; fall through to the browser language.
    }

    return normalize(navigator.language) || "en";
  }

  function applyLanguage(language, persist) {
    const active = normalize(language) || "en";
    const dictionary = I18N[active] || I18N.en;

    currentLanguage = active;
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

    languageButtons.forEach((button) => {
      button.setAttribute("aria-pressed", String(button.dataset.setLang === active));
    });

    if (persist) {
      try {
        window.localStorage.setItem(STORAGE_KEY, active);
      } catch {
        // Best effort only.
      }

      try {
        const url = new URL(window.location.href);
        if (active === "en") {
          url.searchParams.delete("lang");
        } else {
          url.searchParams.set("lang", active);
        }
        window.history.replaceState(null, "", url.toString());
      } catch {
        // History may be restricted in embedded browsers.
      }
    }
  }

  function switchLanguage(language) {
    const next = normalize(language);
    if (!next || next === currentLanguage) {
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

  applyLanguage(detectLanguage(), false);
})();
