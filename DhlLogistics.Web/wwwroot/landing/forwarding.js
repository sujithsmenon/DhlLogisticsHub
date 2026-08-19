// --- Theme System ---
const themeToggle = document.getElementById('theme-toggle');
const body = document.body;

// Load stored theme or default based on the switch state intent
if (localStorage.getItem('theme') === 'dark') {
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

// --- Mobile Navigation ---
const menuToggle = document.querySelector('.menu-toggle');
const navLinks = document.querySelector('.nav-links');

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

// --- High-End Slider Engine ---
let currentSlide = 0;
const slides = document.querySelectorAll('.scene');
const wrapper = document.querySelector('.slider-wrapper');
let isAnimating = false;

function updateSlides() {
    const screenHeight = window.innerHeight;
    
    wrapper.style.transform = `translateY(-${currentSlide * 100}vh)`;

    slides.forEach((slide, index) => {
        const offset = index - currentSlide;
        
        const spans = slide.querySelectorAll('.animated-title span');
        spans.forEach(span => {
            const speed = parseFloat(span.getAttribute('data-speed')) || 0;
            if (speed !== 0) {
                const translateY = offset * speed * screenHeight; 
                span.style.transform = `translateY(${translateY}px)`;
            }
        });

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
    
    setTimeout(() => {
        isAnimating = false;
    }, 1200); 
}

window.addEventListener('wheel', (e) => {
    if (isAnimating) return; 

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

let touchStartY = 0;

window.addEventListener('touchstart', (e) => {
    touchStartY = e.touches[0].clientY;
}, {passive: true});

window.addEventListener('touchend', (e) => {
    if (isAnimating) return;

    let touchEndY = e.changedTouches[0].clientY;
    let swipeDistance = touchStartY - touchEndY;
    
    if (Math.abs(swipeDistance) < 50) return;

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

const homeBtn = document.querySelector('.home-btn');
if (homeBtn) {
    homeBtn.addEventListener('click', (e) => {
        if (homeBtn.getAttribute('href') === '#') {
            e.preventDefault(); 
            if (isAnimating || currentSlide === 0) return; 
            currentSlide = 0;
            triggerAnimation();
        }
    });
}

updateSlides();