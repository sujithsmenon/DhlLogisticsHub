// --- Theme System & Elements ---
const themeToggle = document.getElementById('theme-toggle');
const body = document.body;
const navbar = document.getElementById('navbar');
const menuToggle = document.querySelector('.menu-toggle');
const navLinks = document.querySelector('.nav-links');

// Map localStorage exactly as implemented in forwarding.js
if (localStorage.getItem('theme') === 'dark') {
    body.classList.add('dark-mode');
    if (themeToggle) themeToggle.checked = true;
} else if (localStorage.getItem('theme') === 'light') {
    body.classList.remove('dark-mode');
    if (themeToggle) themeToggle.checked = false;
}

if (themeToggle) {
    themeToggle.addEventListener('change', () => {
        if (themeToggle.checked) {
            body.classList.add('dark-mode');
            localStorage.setItem('theme', 'dark');
        } else {
            body.classList.remove('dark-mode');
            localStorage.setItem('theme', 'light');
        }
        
        // Dynamically update mobile menu appearance if currently open
        const isDisplayed = window.getComputedStyle(navLinks).display !== 'none';
        if (isDisplayed && navLinks.style.position === 'absolute') {
            navLinks.style.backgroundColor = themeToggle.checked ? '#030508' : '#e6f0fa';
            navLinks.style.borderTop = themeToggle.checked ? '1px solid rgba(255,255,255,0.1)' : '1px solid rgba(0,0,0,0.1)';
        }
    });
}

// --- Navbar Scroll Behavior ---
window.addEventListener('scroll', () => {
    if (window.scrollY > 50) {
        navbar.classList.add('scrolled');
    } else {
        navbar.classList.remove('scrolled');
    }
});

// --- Mobile Menu Toggle ---
menuToggle.addEventListener('click', function() {
    const isDisplayed = window.getComputedStyle(navLinks).display !== 'none';
    const isDarkMode = body.classList.contains('dark-mode');
    
    if (isDisplayed && navLinks.style.position === 'absolute') {
        navLinks.style.display = 'none';
    } else {
        navLinks.style.display = 'flex';
        navLinks.style.flexDirection = 'column';
        navLinks.style.position = 'absolute';
        navLinks.style.top = '100%';
        navLinks.style.left = '0';
        navLinks.style.width = '100%';
        navLinks.style.backgroundColor = isDarkMode ? '#030508' : '#e6f0fa';
        navLinks.style.padding = '2rem 5%';
        navLinks.style.boxShadow = '0 10px 20px rgba(0,0,0,0.5)';
        navLinks.style.borderTop = isDarkMode ? '1px solid rgba(255,255,255,0.1)' : '1px solid rgba(0,0,0,0.1)';
    }
});

// --- Element Selectors for Parallax ---
const bgLayer = document.getElementById('bg-layer');
const starsLayer = document.getElementById('stars-layer');
const planet1 = document.getElementById('planet-1');

const scene1 = document.getElementById('scene-1');
const scene2 = document.getElementById('scene-2');
const scene3 = document.getElementById('scene-3');
const scene4 = document.getElementById('scene-4');
const scene5 = document.getElementById('scene-5');
const scene6 = document.getElementById('scene-6');
const scene6Title = document.getElementById('scene-6-title');

// --- Timeline Elements ---
const timelineProgress = document.getElementById('timeline-progress');
const nodes = [
    document.getElementById('node-1'),
    document.getElementById('node-2'),
    document.getElementById('node-3'),
    document.getElementById('node-4'),
    document.getElementById('node-5'),
    document.getElementById('node-6')
];

// ========================================================
// BUTTERY SMOOTH SCROLL ENGINE (LERP)
// Decouples browser scroll ticks from visual animation 
// ========================================================

let targetScroll = window.scrollY;
let currentScroll = window.scrollY;

// 1. Listen for the browser scroll, but only record the target position
window.addEventListener('scroll', () => {
    targetScroll = window.scrollY;
});

// 2. Continuous Animation Loop (requestAnimationFrame)
function animateParallax() {
    // Linear Interpolation (LERP) factor. Lower = smoother/slower, Higher = snappier
    currentScroll += (targetScroll - currentScroll) * 0.08;
    
    // Calculate max scroll for Parallax ONLY (excluding the footer area)
    const spacer = document.querySelector('.parallax-spacer');
    // Guard against errors if spacer isn't loaded yet
    if (!spacer) {
        requestAnimationFrame(animateParallax);
        return;
    }
    
    const parallaxMaxScroll = spacer.offsetHeight - window.innerHeight;
    
    // Lock progress between 0 and 1.0 (This stops the 3D scene from moving while the footer slides up)
    let progress = currentScroll / parallaxMaxScroll; 
    if (progress > 1) progress = 1;
    if (progress < 0) progress = 0;

    // --- TIMELINE LOGIC ---
    // CSS variable handles both mobile width and desktop height dynamically!
    timelineProgress.style.setProperty('--scroll-progress', `${progress * 100}%`);

    nodes.forEach(node => node.classList.remove('active'));

    if (progress < 0.12) {
        nodes[0].classList.add('active');
    } else if (progress >= 0.12 && progress < 0.28) {
        nodes[1].classList.add('active');
    } else if (progress >= 0.28 && progress < 0.44) {
        nodes[2].classList.add('active');
    } else if (progress >= 0.44 && progress < 0.60) {
        nodes[3].classList.add('active');
    } else if (progress >= 0.60 && progress < 0.75) {
        nodes[4].classList.add('active');
    } else {
        nodes[5].classList.add('active');
    }

    // --- BACKGROUND LOGIC (Freezes when footer is scrolled) ---
    let clampedScroll = progress * parallaxMaxScroll;
    bgLayer.style.transform = `translateY(${clampedScroll * -0.05}px)`;
    starsLayer.style.transform = `translateY(${clampedScroll * -0.15}px)`;
    planet1.style.transform = `rotate(${clampedScroll * 0.03}deg)`; 

    // --- SCENE 1 (0% to 15%) ---
    let s1Opacity = Math.max(0, 1 - (progress / 0.12));
    scene1.style.opacity = s1Opacity;
    
    let s1Blur = progress * 100; 
    let s1Scale = Math.max(0.5, 1 - (progress * 2));
    scene1.style.filter = `blur(${s1Blur}px)`;
    scene1.style.transform = `translateY(${clampedScroll * -0.5}px) scale(${s1Scale})`;

    // --- SCENE 2 (12% to 30%) ---
    if (progress > 0.12 && progress < 0.30) {
        let sceneProgress = (progress - 0.12) / 0.18; 
        
        let opacity = 1;
        if (sceneProgress < 0.2) opacity = sceneProgress / 0.2;     
        if (sceneProgress > 0.8) opacity = (1 - sceneProgress) / 0.2; 
        scene2.style.opacity = opacity;
        
        let xPos = 0;
        let skew = 0;
        
        if (sceneProgress <= 0.35) {
            let enterProgress = sceneProgress / 0.35;
            xPos = (1 - enterProgress) * 100; 
            skew = (1 - enterProgress) * 30;
        } 
        else if (sceneProgress > 0.35 && sceneProgress < 0.65) {
            xPos = 0;
            skew = 0;
        } 
        else {
            let exitProgress = (sceneProgress - 0.65) / 0.35;
            xPos = exitProgress * -100; 
            skew = exitProgress * -30;
        }
        
        scene2.style.transform = `translateX(${xPos}vw) skewX(${skew}deg)`;
    } else {
        scene2.style.opacity = 0;
    }

    // --- SCENE 3 (28% to 46%) ---
    if (progress > 0.28 && progress < 0.46) {
        let sceneProgress = (progress - 0.28) / 0.18; 
        
        let opacity = 1;
        if (sceneProgress < 0.2) opacity = sceneProgress / 0.2;
        if (sceneProgress > 0.8) opacity = (1 - sceneProgress) / 0.2;
        scene3.style.opacity = opacity;
        
        let rotX = 0;
        let scale = 1;
        
        if (sceneProgress <= 0.35) {
            let enterProgress = sceneProgress / 0.35;
            rotX = (1 - enterProgress) * -90; 
        } else if (sceneProgress > 0.35 && sceneProgress < 0.65) {
            rotX = 0;
            scale = 1; 
        } else {
            let exitProgress = (sceneProgress - 0.65) / 0.35;
            rotX = exitProgress * 90;
        }
        
        scene3.style.transform = `rotateX(${rotX}deg) scale(${scale})`;
    } else {
        scene3.style.opacity = 0;
    }

    // --- SCENE 4 (44% to 62%) ---
    if (progress > 0.44 && progress < 0.62) {
        let sceneProgress = (progress - 0.44) / 0.18; 
        
        let opacity = 1;
        if (sceneProgress < 0.2) opacity = sceneProgress / 0.2;
        if (sceneProgress > 0.8) opacity = (1 - sceneProgress) / 0.2;
        scene4.style.opacity = opacity;
        
        let rotX = 0;
        let scale = 1;
        
        if (sceneProgress <= 0.35) {
            let enterProgress = sceneProgress / 0.35;
            rotX = (1 - enterProgress) * -90; 
        } else if (sceneProgress > 0.35 && sceneProgress < 0.65) {
            rotX = 0;
            scale = 1; 
        } else {
            let exitProgress = (sceneProgress - 0.65) / 0.35;
            rotX = exitProgress * 90;
        }
        
        scene4.style.transform = `rotateX(${rotX}deg) scale(${scale})`;
    } else {
        scene4.style.opacity = 0;
    }

    // --- SCENE 5 (60% to 78%) ---
    if (progress > 0.60 && progress < 0.78) {
        let sceneProgress = (progress - 0.60) / 0.18; 
        
        let opacity = 1;
        if (sceneProgress < 0.2) opacity = sceneProgress / 0.2;
        if (sceneProgress > 0.8) opacity = (1 - sceneProgress) / 0.2;
        scene5.style.opacity = opacity;
        
        let yPos = 0;
        let scale = 1;
        if (sceneProgress <= 0.35) {
            let enterProgress = sceneProgress / 0.35;
            yPos = (1 - enterProgress) * 50; 
        } else if (sceneProgress > 0.35 && sceneProgress < 0.65) {
            yPos = 0;
            scale = 1;
        } else {
            let exitProgress = (sceneProgress - 0.65) / 0.35;
            yPos = exitProgress * -50;
        }
        
        scene5.style.transform = `translateY(${yPos}vh) scale(${scale})`;
    } else {
        scene5.style.opacity = 0;
    }

    // --- SCENE 6 (75% to 100%) ---
    if (progress > 0.75) {
        let sceneProgress = (progress - 0.75) / 0.25; 
        
        scene6.style.opacity = Math.min(1, sceneProgress * 4);
        
        let blur = Math.max(0, 20 - (sceneProgress * 30));
        scene6.style.filter = `blur(${blur}px)`;
        
        let spacing = Math.max(0, 40 - (sceneProgress * 50));
        scene6Title.style.letterSpacing = `${spacing}px`;
        
        let scale = 1;
        scene6.style.transform = `scale(${scale})`;
    } else {
        scene6.style.opacity = 0;
    }
    
    // Call next frame
    requestAnimationFrame(animateParallax);
}

// Start the continuous animation loop
animateParallax();