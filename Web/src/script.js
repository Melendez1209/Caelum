document.addEventListener('DOMContentLoaded', () => {
  // ===== Theme System =====
  function getSystemTheme() {
    return window.matchMedia('(prefers-color-scheme: dark)').matches
      ? 'dark'
      : 'light';
  }

  function applyTheme(theme) {
    const actualTheme = theme === 'system' ? getSystemTheme() : theme;
    document.documentElement.setAttribute('data-theme', actualTheme);
    document.documentElement.setAttribute('data-theme-setting', theme);
    updateThemeIcon(theme);
    updateSelectedTheme(theme);
  }

  function updateThemeIcon(theme) {
    document.querySelectorAll('.theme-selector').forEach((selector) => {
      const icons = {
        dark: selector.querySelector('.dark-icon'),
        light: selector.querySelector('.light-icon'),
        system: selector.querySelector('.system-icon'),
      };

      if (theme === 'system') {
        icons.system.style.display = 'inline-block';
        icons.dark.style.display = 'none';
        icons.light.style.display = 'none';
      } else if (theme === 'dark') {
        icons.dark.style.display = 'inline-block';
        icons.light.style.display = 'none';
        icons.system.style.display = 'none';
      } else {
        icons.light.style.display = 'inline-block';
        icons.dark.style.display = 'none';
        icons.system.style.display = 'none';
      }
    });
  }

  function updateSelectedTheme(theme) {
    document.querySelectorAll('.theme-dropdown').forEach((dropdown) => {
      dropdown.querySelectorAll('button').forEach((btn) => {
        btn.removeAttribute('data-selected');
      });
      const selectedButton = dropdown.querySelector(
        `button[data-theme="${theme}"]`,
      );
      if (selectedButton) {
        selectedButton.setAttribute('data-selected', 'true');
      }
    });
  }

  const savedTheme = localStorage.getItem('theme') || 'system';
  applyTheme(savedTheme);

  document.querySelectorAll('.theme-dropdown').forEach((dropdown) => {
    dropdown.addEventListener('click', (e) => {
      const button = e.target.closest('button');
      if (!button) return;
      const theme = button.dataset.theme;
      localStorage.setItem('theme', theme);
      applyTheme(theme);
    });
  });

  window
    .matchMedia('(prefers-color-scheme: dark)')
    .addEventListener('change', () => {
      const currentThemeSetting =
        document.documentElement.getAttribute('data-theme-setting');
      if (currentThemeSetting === 'system') {
        applyTheme('system');
      }
    });

  // ===== Language Selection =====
  const savedLang = localStorage.getItem('lang') || 'zh';
  updateSelectedLang(savedLang);

  function updateSelectedLang(lang) {
    document.querySelectorAll('.lang-dropdown').forEach((dropdown) => {
      dropdown.querySelectorAll('button').forEach((btn) => {
        btn.removeAttribute('data-selected');
      });
      const selectedButton = dropdown.querySelector(
        `button[data-lang="${lang}"]`,
      );
      if (selectedButton) {
        selectedButton.setAttribute('data-selected', 'true');
      }
    });
  }

  // ===== Dropdown Toggle (touch-friendly) =====
  function closeAllDropdowns() {
    document
      .querySelectorAll('.theme-dropdown, .lang-dropdown')
      .forEach((dropdown) => dropdown.classList.remove('show'));
  }

  document.querySelectorAll('.theme-toggle').forEach((toggle) => {
    toggle.addEventListener('click', (e) => {
      e.stopPropagation();
      const dropdown = toggle
        .closest('.theme-selector')
        .querySelector('.theme-dropdown');
      const wasOpen = dropdown.classList.contains('show');
      closeAllDropdowns();
      if (!wasOpen) dropdown.classList.add('show');
    });
  });

  document.querySelectorAll('.lang-toggle').forEach((toggle) => {
    toggle.addEventListener('click', (e) => {
      e.stopPropagation();
      const dropdown = toggle
        .closest('.lang-selector')
        .querySelector('.lang-dropdown');
      const wasOpen = dropdown.classList.contains('show');
      closeAllDropdowns();
      if (!wasOpen) dropdown.classList.add('show');
    });
  });

  document.addEventListener('click', closeAllDropdowns);

  // ===== Particle System =====
  function createParticles() {
    const container = document.getElementById('particles');
    if (!container) return;

    const particleCount = window.innerWidth < 768 ? 15 : 30;

    for (let i = 0; i < particleCount; i++) {
      const particle = document.createElement('div');
      particle.classList.add('particle');
      const size = Math.random() * 10 + 4;
      particle.style.width = size + 'px';
      particle.style.height = size + 'px';
      particle.style.left = Math.random() * 100 + '%';
      particle.style.animationDuration = Math.random() * 10 + 8 + 's';
      particle.style.animationDelay = Math.random() * 10 + 's';
      container.appendChild(particle);
    }
  }
  createParticles();

  // ===== SVG Illustrations (loaded inline to inherit theme variables) =====
  document.querySelectorAll('[data-svg]').forEach((container) => {
    fetch(container.dataset.svg)
      .then((res) => {
        if (!res.ok) throw new Error(`Failed to load ${container.dataset.svg}`);
        return res.text();
      })
      .then((svgText) => {
        container.innerHTML = svgText;
      })
      .catch((err) => console.error('Failed to load SVG:', err));
  });

  // ===== Scroll Reveal (Intersection Observer) =====
  const revealElements = document.querySelectorAll('.scroll-reveal');

  const revealObserver = new IntersectionObserver(
    (entries) => {
      entries.forEach((entry) => {
        if (entry.isIntersecting) {
          const delay = entry.target.dataset.delay || 0;
          setTimeout(() => {
            entry.target.classList.add('revealed');
          }, parseInt(delay));
          revealObserver.unobserve(entry.target);
        }
      });
    },
    {
      threshold: 0.15,
      rootMargin: '0px 0px -50px 0px',
    },
  );

  revealElements.forEach((el) => revealObserver.observe(el));

  // ===== Active Nav Link on Scroll =====
  const sections = document.querySelectorAll('section[id]');
  const navLinks = document.querySelectorAll('.nav-link');

  function updateActiveNav() {
    const scrollY = window.scrollY + 100;

    sections.forEach((section) => {
      const sectionTop = section.offsetTop;
      const sectionHeight = section.offsetHeight;
      const sectionId = section.getAttribute('id');

      if (scrollY >= sectionTop && scrollY < sectionTop + sectionHeight) {
        navLinks.forEach((link) => {
          link.classList.remove('active');
          if (link.getAttribute('href') === '#' + sectionId) {
            link.classList.add('active');
          }
        });
      }
    });
  }

  // ===== Nav Shadow on Scroll =====
  const nav = document.querySelector('nav');
  const navIndicator = document.querySelector('.nav-indicator');

  function updateNavStyle() {
    if (window.scrollY > 50) {
      nav.classList.add('scrolled');
    } else {
      nav.classList.remove('scrolled');
    }
  }

  // ===== Sliding Nav Indicator =====
  function updateNavIndicator() {
    if (!navIndicator || !nav) return;
    const activeLink = document.querySelector('nav ul .nav-link.active');
    if (!activeLink) {
      navIndicator.style.opacity = '0';
      return;
    }
    const navRect = nav.getBoundingClientRect();
    const linkRect = activeLink.getBoundingClientRect();
    navIndicator.style.opacity = '1';
    navIndicator.style.left = linkRect.left - navRect.left - 10 + 'px';
    navIndicator.style.top = linkRect.top - navRect.top - 4 + 'px';
    navIndicator.style.width = linkRect.width + 20 + 'px';
    navIndicator.style.height = linkRect.height + 8 + 'px';
  }

  // ===== Scrollytelling Logo: hero background -> nav -> About =====
  const homeSection = document.getElementById('home');
  const featuresSection = document.getElementById('features');
  const aboutSection = document.getElementById('about');
  const aboutLogoColumn =
    aboutSection && aboutSection.querySelector('.about-logo-column');
  const aboutContent =
    aboutSection && aboutSection.querySelector('.about-content');
  const logo = document.querySelector('.logo');
  const navUl = nav.querySelector('ul');
  const LOGO_FLY_DURATION = 600;
  const HERO_LOGO_SCALE = 10;
  const ABOUT_LOGO_SCALE = 3;
  const HERO_LOGO_OPACITY = 0.4;
  // Mirror the nav max-width values in css/index.css for scroll-linked scrubbing
  const NAV_MAX_WIDE = 1100;
  const NAV_MAX_COMPACT = 780;
  const clamp01 = (value) => Math.min(1, Math.max(0, value));
  const lerp = (from, to, t) => from + (to - from) * t;
  const LOGO_STATE = { HERO: 'hero', NAV: 'nav', ABOUT: 'about' };
  let logoState = LOGO_STATE.NAV;
  let logoCurrent = null;
  let logoAnimating = false;
  let pendingLogoState = null;
  const isNarrowScreen = () => window.matchMedia('(max-width: 768px)').matches;

  function isSectionActive(section) {
    if (!section) return false;
    const scrollY = window.scrollY + 100;
    const top = section.offsetTop;
    return scrollY >= top && scrollY < top + section.offsetHeight;
  }

  function aboutTargetViewport() {
    const colRect = aboutLogoColumn.getBoundingClientRect();
    const contentRect = aboutContent.getBoundingClientRect();
    return {
      left: colRect.left + colRect.width / 2 - logo.offsetWidth / 2,
      top: contentRect.top + contentRect.height / 2 - logo.offsetHeight / 2,
    };
  }

  // The links are always absolutely positioned: "compact" pins them to the
  // left padding, "wide" centres them in the viewport
  function navUlCompactLeft() {
    const padLeft = parseFloat(getComputedStyle(nav).paddingLeft) || 0;
    return padLeft + navUl.offsetWidth / 2;
  }

  function navUlWideLeft() {
    return nav.getBoundingClientRect().width / 2;
  }

  // Measure the expanded nav slot without a visual flash or a width tween
  function logoSlotViewport() {
    const wasCompact = nav.classList.contains('nav-compact');
    const prevTransition = nav.style.transition;
    const prevMaxWidth = nav.style.maxWidth;
    nav.style.transition = 'none';
    nav.style.maxWidth = '';
    nav.classList.remove('nav-compact');
    const navRect = nav.getBoundingClientRect();
    const padLeft = parseFloat(getComputedStyle(nav).paddingLeft) || 0;
    const slot = {
      left: navRect.left + padLeft,
      top: navRect.top + (navRect.height - logo.offsetHeight) / 2,
    };
    nav.classList.toggle('nav-compact', wasCompact);
    nav.style.maxWidth = prevMaxWidth;
    nav.style.transition = prevTransition;
    return slot;
  }

  function heroTargetViewport() {
    return {
      left: (window.innerWidth - logo.offsetWidth) / 2,
      top: (window.innerHeight - logo.offsetHeight) / 2,
      scale: HERO_LOGO_SCALE,
      opacity: HERO_LOGO_OPACITY,
      zIndex: 1,
      pointerEvents: 'none',
    };
  }

  function targetFor(state) {
    if (state === LOGO_STATE.HERO) return heroTargetViewport();
    if (state === LOGO_STATE.ABOUT) {
      return {
        ...aboutTargetViewport(),
        scale: ABOUT_LOGO_SCALE,
        opacity: 1,
        zIndex: 1001,
        pointerEvents: 'auto',
      };
    }
    return {
      ...logoSlotViewport(),
      scale: 1,
      opacity: 1,
      zIndex: 1001,
      pointerEvents: 'auto',
    };
  }

  function applyLogoTarget(target) {
    logo.style.left = target.left + 'px';
    logo.style.top = target.top + 'px';
    logo.style.transform = `scale(${target.scale})`;
    logo.style.opacity = String(target.opacity);
    logo.style.zIndex = String(target.zIndex);
    logo.style.pointerEvents = target.pointerEvents;
    logoCurrent = { ...target };
  }

  function cancelLogoAnimations() {
    logo.getAnimations().forEach((anim) => anim.cancel());
  }

  function setNavCompact(compact) {
    nav.classList.toggle('nav-compact', compact);
  }

  function resetLogoToNav() {
    cancelLogoAnimations();
    if (logo.parentNode !== nav) nav.prepend(logo);
    logo.classList.remove('logo-fixed');
    logo.style.left = '';
    logo.style.top = '';
    logo.style.transform = '';
    logo.style.opacity = '';
    logo.style.zIndex = '';
    logo.style.pointerEvents = '';
    nav.style.maxWidth = '';
    nav.style.transition = '';
    navUl.style.left = '';
    navUl.style.transition = '';
    setNavCompact(false);
    logoState = LOGO_STATE.NAV;
    logoCurrent = null;
    logoAnimating = false;
  }

  function completeTransition(state) {
    if (state === LOGO_STATE.NAV) {
      resetLogoToNav();
    } else {
      applyLogoTarget(targetFor(state));
      logoState = state;
      logoAnimating = false;
    }
    if (pendingLogoState && pendingLogoState !== logoState) {
      const next = pendingLogoState;
      pendingLogoState = null;
      moveLogoTo(next);
    } else {
      pendingLogoState = null;
    }
  }

  function animateLogo(from, to, onDone) {
    cancelLogoAnimations();
    const anim = logo.animate(
      [
        {
          left: from.left + 'px',
          top: from.top + 'px',
          transform: `scale(${from.scale})`,
          opacity: String(from.opacity),
        },
        {
          left: to.left + 'px',
          top: to.top + 'px',
          transform: `scale(${to.scale})`,
          opacity: String(to.opacity),
        },
      ],
      {
        duration: LOGO_FLY_DURATION,
        easing: 'cubic-bezier(0.16, 1, 0.3, 1)',
        fill: 'both',
      },
    );
    anim.onfinish = () => {
      anim.cancel();
      onDone();
    };
  }

  function moveLogoTo(state, instant = false) {
    if (!logo || state === logoState) return;
    if (isNarrowScreen()) {
      if (logoState !== LOGO_STATE.NAV) resetLogoToNav();
      return;
    }
    if (logoAnimating && !instant) {
      pendingLogoState = state;
      return;
    }

    const from =
      logoState === LOGO_STATE.NAV
        ? targetFor(LOGO_STATE.NAV)
        : { ...logoCurrent };

    if (logo.parentNode !== document.body) {
      document.body.appendChild(logo);
      logo.classList.add('logo-fixed');
    }
    applyLogoTarget(from);
    // Keep the logo above the nav and sections while it is in flight
    logo.style.zIndex = '1001';
    logo.style.pointerEvents = 'none';

    const to = targetFor(state);
    setNavCompact(state !== LOGO_STATE.NAV);
    // Let the CSS left transition carry the links alongside the timed flight
    navUl.style.transition = '';
    navUl.style.left =
      state === LOGO_STATE.NAV ? '' : navUlCompactLeft() + 'px';

    if (instant) {
      completeTransition(state);
      return;
    }

    logoAnimating = true;
    animateLogo(from, to, () => completeTransition(state));
  }

  // Scrub the Logo and nav width directly from scroll progress so the flight
  // happens together with the page movement instead of after the snap finishes
  function scrubHeroToNav(progress) {
    const p = clamp01(progress);
    const eased = p * p * (3 - 2 * p);

    if (logo.parentNode !== document.body) {
      document.body.appendChild(logo);
      logo.classList.add('logo-fixed');
    }
    cancelLogoAnimations();

    if (p >= 1) {
      resetLogoToNav();
      return;
    }

    if (p <= 0) {
      nav.style.transition = '';
      nav.style.maxWidth = '';
      setNavCompact(true);
      applyLogoTarget(heroTargetViewport());
      navUl.style.transition = 'none';
      navUl.style.left = navUlCompactLeft() + 'px';
      logoState = LOGO_STATE.HERO;
      return;
    }

    const hero = heroTargetViewport();
    const slot = logoSlotViewport();
    applyLogoTarget({
      left: lerp(hero.left, slot.left, eased),
      top: lerp(hero.top, slot.top, eased),
      scale: lerp(hero.scale, 1, eased),
      opacity: lerp(hero.opacity, 1, eased),
      zIndex: 1001,
      pointerEvents: 'none',
    });

    nav.style.transition = 'none';
    nav.classList.remove('nav-compact');
    nav.style.maxWidth =
      NAV_MAX_COMPACT + (NAV_MAX_WIDE - NAV_MAX_COMPACT) * eased + 'px';
    navUl.style.transition = 'none';
    navUl.style.left = lerp(navUlCompactLeft(), navUlWideLeft(), eased) + 'px';
    logoState = LOGO_STATE.HERO;
  }

  // Scrub the Logo out of the bar toward the About copy, in step with scroll
  function scrubNavToAbout(progress) {
    const p = clamp01(progress);
    const eased = p * p * (3 - 2 * p);

    if (logo.parentNode !== document.body) {
      document.body.appendChild(logo);
      logo.classList.add('logo-fixed');
    }
    cancelLogoAnimations();

    if (p <= 0) {
      resetLogoToNav();
      return;
    }

    const slot = logoSlotViewport();
    const about = aboutTargetViewport();
    applyLogoTarget({
      left: lerp(slot.left, about.left, eased),
      top: lerp(slot.top, about.top, eased),
      scale: lerp(1, ABOUT_LOGO_SCALE, eased),
      opacity: 1,
      zIndex: 1001,
      pointerEvents: 'none',
    });

    nav.style.transition = 'none';
    nav.style.maxWidth =
      NAV_MAX_WIDE - (NAV_MAX_WIDE - NAV_MAX_COMPACT) * eased + 'px';
    navUl.style.transition = 'none';
    navUl.style.left = lerp(navUlWideLeft(), navUlCompactLeft(), eased) + 'px';
    logoState = LOGO_STATE.ABOUT;
  }

  function updateLogoForScroll(instant = false) {
    if (!logo) return;
    if (isNarrowScreen()) {
      if (logoState !== LOGO_STATE.NAV) resetLogoToNav();
      return;
    }
    if (logoAnimating) return;

    const scrollY = window.scrollY;
    const homeEnd = featuresSection ? featuresSection.offsetTop : 0;
    const aboutTop = aboutSection ? aboutSection.offsetTop : Infinity;
    const aboutBottom = aboutSection
      ? aboutTop + aboutSection.offsetHeight
      : Infinity;
    const aboutStart = aboutTop - window.innerHeight;

    // Hero -> second page
    if (
      homeEnd > 0 &&
      scrollY <= homeEnd &&
      (logoState === LOGO_STATE.HERO || logoState === LOGO_STATE.NAV)
    ) {
      scrubHeroToNav(scrollY / homeEnd);
      return;
    }

    // Final viewport before About: fly out of the bar with the page
    if (scrollY > homeEnd && scrollY <= aboutTop && aboutStart > homeEnd) {
      const p = (scrollY - aboutStart) / (aboutTop - aboutStart);
      if (p > 0 || logoState === LOGO_STATE.ABOUT) {
        scrubNavToAbout(p);
        return;
      }
    }

    // Inside About: keep the Logo pinned beside the description
    if (scrollY >= aboutTop && scrollY < aboutBottom) {
      if (logoState === LOGO_STATE.ABOUT) {
        applyLogoTarget(targetFor(LOGO_STATE.ABOUT));
        navUl.style.transition = 'none';
        navUl.style.left = navUlCompactLeft() + 'px';
      } else {
        scrubNavToAbout(1);
      }
      return;
    }

    // Past About (footer) or a direct jump: return to the bar
    if (logoState === LOGO_STATE.ABOUT || logoState === LOGO_STATE.HERO) {
      resetLogoToNav();
    }
  }

  // ===== Parallax Effect for Hero =====
  const hero = document.querySelector('.hero');

  function updateParallax() {
    if (!hero) return;
    const scrollY = window.scrollY;
    const heroHeight = hero.offsetHeight;

    if (scrollY < heroHeight) {
      const heroContent = hero.querySelector('.hero-content');
      if (heroContent) {
        heroContent.style.transform = `translateY(${scrollY * 0.3}px)`;
        heroContent.style.opacity = 1 - scrollY / heroHeight;
      }
    }
  }

  // ===== Smooth Scroll for Nav Links =====
  navLinks.forEach((link) => {
    link.addEventListener('click', (e) => {
      e.preventDefault();
      const targetId = link.getAttribute('href');
      const targetSection = document.querySelector(targetId);
      if (targetSection) {
        window.scrollTo({
          top: targetSection.offsetTop,
          behavior: 'smooth',
        });
      }
    });
  });

  // CTA buttons smooth scroll
  document.querySelectorAll('.cta-primary, .cta-secondary').forEach((btn) => {
    btn.addEventListener('click', (e) => {
      e.preventDefault();
      const targetId = btn.getAttribute('href');
      const targetSection = document.querySelector(targetId);
      if (targetSection) {
        window.scrollTo({
          top: targetSection.offsetTop,
          behavior: 'smooth',
        });
      }
    });
  });

  // ===== Scroll Event Handler (throttled) =====
  let ticking = false;

  window.addEventListener('scroll', () => {
    if (!ticking) {
      window.requestAnimationFrame(() => {
        updateActiveNav();
        updateNavStyle();
        updateParallax();
        updateLogoForScroll();
        updateNavIndicator();
        ticking = false;
      });
      ticking = true;
    }
  });

  // Initial calls
  updateActiveNav();
  updateNavStyle();
  updateLogoForScroll(true);
  updateNavIndicator();

  // Translated link labels change the links' width, so re-measure them
  document.addEventListener('langchange', () => {
    updateLogoForScroll(true);
    updateNavIndicator();
  });

  window.addEventListener('resize', () => {
    if (!logo) return;
    if (isNarrowScreen()) {
      if (logoState !== LOGO_STATE.NAV) resetLogoToNav();
      updateNavIndicator();
      return;
    }
    if (logoState === LOGO_STATE.ABOUT) {
      if (!logoAnimating) scrubNavToAbout(1);
    } else {
      updateLogoForScroll(true);
      if (logoState === LOGO_STATE.NAV) {
        navUl.style.left = '';
        navUl.style.transition = '';
      }
    }
    updateNavIndicator();
  });

  // ===== Changelog: Fetch from GitHub =====
  const GITHUB_REPO = 'Melendez1209/Caelum';
  const GITHUB_API = `https://api.github.com/repos/${GITHUB_REPO}`;
  const MAX_ITEMS = 6;

  async function fetchChangelog() {
    const timeline = document.getElementById('changelog-timeline');
    if (!timeline) return;

    try {
      let items = [];

      // Try releases first
      const releasesRes = await fetch(
        `${GITHUB_API}/releases?per_page=${MAX_ITEMS}`,
      );
      if (releasesRes.ok) {
        const releases = await releasesRes.json();
        if (releases.length > 0) {
          items = releases.slice(0, MAX_ITEMS).map((r) => ({
            version: r.tag_name,
            title: r.name || r.tag_name,
            date: new Date(r.published_at).toLocaleDateString('zh-CN', {
              year: 'numeric',
              month: '2-digit',
            }),
            body: r.body || '',
            url: r.html_url,
            isRelease: true,
          }));
        }
      }

      // Fallback to commits if no releases
      if (items.length === 0) {
        const commitsRes = await fetch(
          `${GITHUB_API}/commits?sha=main&per_page=${MAX_ITEMS}`,
        );
        if (commitsRes.ok) {
          const commits = await commitsRes.json();
          items = commits.slice(0, MAX_ITEMS).map((c) => ({
            version: c.sha.slice(0, 7),
            title: c.commit.message.split('\n')[0],
            date: new Date(c.commit.author.date).toLocaleDateString('zh-CN', {
              year: 'numeric',
              month: '2-digit',
            }),
            body: c.commit.message.split('\n').slice(1).join('\n').trim(),
            url: c.html_url,
            isRelease: false,
          }));
        }
      }

      if (items.length === 0) {
        timeline.innerHTML = `<p class="changelog-empty" data-i18n="changelog.empty">No changelog available yet.</p>`;
        return;
      }

      renderTimeline(timeline, items);
    } catch (err) {
      console.error('Failed to load changelog:', err);
      timeline.innerHTML = `<p class="changelog-empty" data-i18n="changelog.error">Failed to load changelog.</p>`;
    }
  }

  function renderTimeline(container, items) {
    container.innerHTML = '';

    items.forEach((item, index) => {
      const div = document.createElement('div');
      div.className = 'timeline-item';
      div.dataset.index = String(index);
      div.style.transitionDelay = index * 120 + 'ms';

      const bodyHtml = item.body
        ? `<div class="timeline-body">${escapeHtml(item.body).replace(/\n/g, '<br>')}</div>`
        : '';

      div.innerHTML = `
                <div class="timeline-dot"></div>
                <div class="timeline-content">
                    <div class="timeline-header">
                        <span class="timeline-version">${escapeHtml(item.version)}</span>
                        <span class="timeline-date">${escapeHtml(item.date)}</span>
                    </div>
                    <h3>${escapeHtml(item.title)}</h3>
                    ${bodyHtml}
                    <a href="${escapeHtml(item.url)}" target="_blank" class="timeline-link">
                        <i class="fab fa-github"></i>
                        ${item.isRelease ? 'View Release' : 'View Commit'}
                    </a>
                </div>
            `;
      container.appendChild(div);
    });

    // Scroll sync: highlight closest item
    container.addEventListener('scroll', () => {
      const allItems = container.querySelectorAll('.timeline-item');
      const containerRect = container.getBoundingClientRect();
      const center = containerRect.left + containerRect.width / 2;

      let closestIdx = 0;
      let closestDist = Infinity;
      allItems.forEach((item, i) => {
        const rect = item.getBoundingClientRect();
        const itemCenter = rect.left + rect.width / 2;
        const dist = Math.abs(itemCenter - center);
        if (dist < closestDist) {
          closestDist = dist;
          closestIdx = i;
        }
      });

      allItems.forEach((item, i) => {
        item.classList.toggle('active', i === closestIdx);
      });
    });

    // Observe changelog section for slide-in animation (replays every time)
    const changelogSection = document.getElementById('changelog');
    if (changelogSection) {
      const sectionObserver = new IntersectionObserver(
        (entries) => {
          entries.forEach((entry) => {
            const items = container.querySelectorAll('.timeline-item');
            if (entry.isIntersecting) {
              items.forEach((el) => el.classList.add('slide-in'));
            } else {
              items.forEach((el) => el.classList.remove('slide-in'));
            }
          });
        },
        { threshold: 0.15 },
      );
      sectionObserver.observe(changelogSection);
    }

    // Size the timeline line to span all items
    updateTimelineLine();
    window.addEventListener('resize', updateTimelineLine);
  }

  function updateTimelineLine() {
    const timeline = document.getElementById('changelog-timeline');
    const line = document.getElementById('changelog-line');
    if (!timeline || !line) return;

    const items = timeline.querySelectorAll('.timeline-item');
    if (items.length === 0) return;

    // Use offsetLeft relative to the timeline container for scrollable content coords
    const firstDot = items[0].querySelector('.timeline-dot');
    const lastDot = items[items.length - 1].querySelector('.timeline-dot');

    const firstCenter = firstDot.offsetLeft + firstDot.offsetWidth / 2;
    const lastCenter = lastDot.offsetLeft + lastDot.offsetWidth / 2;

    line.style.left = firstCenter + 'px';
    line.style.width = lastCenter - firstCenter + 'px';
  }

  function escapeHtml(str) {
    const map = {
      '&': '&amp;',
      '<': '&lt;',
      '>': '&gt;',
      '"': '&quot;',
      "'": '&#039;',
    };
    return String(str).replace(/[&<>"']/g, (c) => map[c]);
  }

  fetchChangelog();

  // ===== Timeline Drag-to-Scroll =====
  function initTimelineDrag() {
    const timeline = document.querySelector('.timeline');
    if (!timeline) return;

    let isDown = false;
    let startX;
    let scrollLeft;

    timeline.addEventListener('mousedown', (e) => {
      isDown = true;
      timeline.style.cursor = 'grabbing';
      startX = e.pageX - timeline.offsetLeft;
      scrollLeft = timeline.scrollLeft;
    });

    timeline.addEventListener('mouseleave', () => {
      isDown = false;
      timeline.style.cursor = '';
    });

    timeline.addEventListener('mouseup', () => {
      isDown = false;
      timeline.style.cursor = '';
    });

    timeline.addEventListener('mousemove', (e) => {
      if (!isDown) return;
      e.preventDefault();
      const x = e.pageX - timeline.offsetLeft;
      const walk = (x - startX) * 1.5;
      timeline.scrollLeft = scrollLeft - walk;
    });
  }

  initTimelineDrag();
});
