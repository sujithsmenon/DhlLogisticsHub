// Theme Toggle Setup
const themeToggle = document.getElementById('theme-toggle');
const body = document.body;

if (localStorage.getItem('theme') === 'light') {
    body.classList.remove('dark-mode');
    if(themeToggle) themeToggle.checked = false;
} else {
    body.classList.add('dark-mode');
    if(themeToggle) themeToggle.checked = true;
}

if(themeToggle) {
    themeToggle.addEventListener('change', () => {
        if (themeToggle.checked) {
            body.classList.add('dark-mode');
            localStorage.setItem('theme', 'dark');
        } else {
            body.classList.remove('dark-mode');
            localStorage.setItem('theme', 'light');
        }
    });
}

// Mobile Menu
const menuToggle = document.querySelector('.menu-toggle');
const navLinks = document.querySelector('.nav-links');

menuToggle.addEventListener('click', function() {
    // Toggling specific 'active' class which maps to CSS dynamic theme background variables
    navLinks.classList.toggle('active');
});

// Slider Engine
let currentSlide = 0;
const slides = document.querySelectorAll('.scene');
const wrapper = document.querySelector('.slider-wrapper');
let isAnimating = false;

function updateSlides() {
    const screenHeight = window.innerHeight;
    
    // Move wrapper
    wrapper.style.transform = `translateY(-${currentSlide * 100}vh)`;

    // Scene animations
    slides.forEach((slide, index) => {
        const offset = index - currentSlide;
        
        // Text Parallax
        const spans = slide.querySelectorAll('.parallax-word span');
        spans.forEach(span => {
            const speed = parseFloat(span.getAttribute('data-speed'));
            if (!isNaN(speed)) {
                const translateY = offset * speed * screenHeight; 
                span.style.transform = `translateY(${translateY}px)`;
            }
        });

        // Cards Fade
        const card = slide.querySelector('.list-card');
        if (card) {
            if (offset === 0) {
                card.style.opacity = '1';
                card.style.transform = 'translateY(0) scale(1)';
            } else {
                card.style.opacity = '0';
                card.style.transform = offset > 0 ? 'translateY(150px) scale(0.95)' : 'translateY(-150px) scale(0.95)';
            }
        }
    });

    // Background Parallax
    const bgLayer = document.getElementById('bg-layer');
    const starsLayer = document.getElementById('stars-layer');
    const planet1 = document.getElementById('planet-1');
    const planet2 = document.getElementById('planet-2');

    let parallaxIndex = currentSlide; 

    if (bgLayer) bgLayer.style.transform = `translateY(${parallaxIndex * 5}vh)`; 
    if (starsLayer) starsLayer.style.transform = `translateY(${parallaxIndex * 8}vh)`;
    if (planet1) planet1.style.transform = `translateY(${parallaxIndex * -6}vh) rotate(${parallaxIndex * 15}deg)`;
    if (planet2) planet2.style.transform = `translateY(${parallaxIndex * 6}vh) rotate(${parallaxIndex * -15}deg)`;
}

function triggerAnimation() {
    isAnimating = true;
    updateSlides();
    setTimeout(() => { isAnimating = false; }, 1200); 
}

// Mouse Wheel
window.addEventListener('wheel', (e) => {
    if (isAnimating) return; 

    // Footer check
    if (currentSlide === slides.length - 1) {
        const footerSlide = slides[currentSlide];
        
        if (e.deltaY < 0 && footerSlide.scrollTop <= 0) {
            currentSlide--;
            triggerAnimation();
        } else {
            return;
        }
    } else {
        if (e.deltaY > 0 && currentSlide < slides.length - 1) {
            currentSlide++;
            triggerAnimation();
        } else if (e.deltaY < 0 && currentSlide > 0) {
            currentSlide--;
            triggerAnimation();
        }
    }
});

// Touch / Swipe
let touchStartY = 0;

window.addEventListener('touchstart', (e) => {
    touchStartY = e.touches[0].clientY;
}, {passive: true});

window.addEventListener('touchend', (e) => {
    if (isAnimating) return;

    let touchEndY = e.changedTouches[0].clientY;
    let swipeDistance = touchStartY - touchEndY;
    
    if (Math.abs(swipeDistance) < 50) return;

    // Footer check
    if (currentSlide === slides.length - 1) {
        const footerSlide = slides[currentSlide];
        
        if (swipeDistance < 0 && footerSlide.scrollTop <= 0) {
            currentSlide--;
            triggerAnimation();
        } else {
            return; 
        }
    } else {
        if (swipeDistance > 0 && currentSlide < slides.length - 1) {
            currentSlide++;
            triggerAnimation();
        } else if (swipeDistance < 0 && currentSlide > 0) {
            currentSlide--;
            triggerAnimation();
        }
    }
});

// Init
updateSlides();