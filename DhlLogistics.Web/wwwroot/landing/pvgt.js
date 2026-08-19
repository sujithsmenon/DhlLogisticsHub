// --- Theme Toggle ---
const themeToggle = document.getElementById('theme-toggle');
const body = document.body;

if (localStorage.getItem('theme') === 'dark') {
    body.classList.add('dark-mode');
    themeToggle.checked = true;
}

themeToggle.addEventListener('change', () => {
    if (themeToggle.checked) {
        body.classList.add('dark-mode');
        localStorage.setItem('theme', 'dark');
    } else {
        body.classList.remove('dark-mode');
        localStorage.setItem('theme', 'light');
    }
});

// --- Hero Widget Tab Switcher ---
function switchTab(element, btnText, placeholderText) {
    document.querySelectorAll('.tab').forEach(tab => tab.classList.remove('active'));
    element.classList.add('active');
    
    const input = document.getElementById('widget-input');
    const btn = document.getElementById('widget-btn');
    
    input.style.opacity = 0;
    btn.style.opacity = 0;
    
    setTimeout(() => {
        input.placeholder = placeholderText;
        btn.innerHTML = `${btnText} <i class="fa-solid fa-arrow-right"></i>`;
        input.style.opacity = 1;
        btn.style.opacity = 1;
    }, 200);
}

// --- Navbar Scroll Behavior ---
const navbar = document.getElementById('navbar');

window.addEventListener('scroll', () => {
    const heroHeight = document.querySelector('.hero').offsetHeight;
    
    if (window.scrollY > (heroHeight - 80)) {
        navbar.classList.add('scrolled');
    } else {
        navbar.classList.remove('scrolled');
    }
});

// --- Hero Background Slideshow, Content Swap & Progress Bar ---
const slides = document.querySelectorAll('.slide');
const indicators = document.querySelectorAll('.indicator'); 
const heroTitle = document.querySelector('.hero h1');
const heroDesc = document.querySelector('.hero-desc');
const heroContentWrapper = document.querySelector('.hero-content');

// Dynamic Content array matched strictly to PVGT Brand Guidelines Tone of Voice
const slideData = [
    {
        title: "TRUSTED LOGISTICS<br>SOLUTIONS.",
        desc: "Delivering trusted logistics solutions with precision and reliability. Based in Kochi, we leverage over six decades of expertise for your global shipping needs."
    },
    {
        title: "GLOBAL MARITIME<br>FREIGHT.",
        desc: "Navigating international waters with comprehensive sea freight forwarding. We manage FCL, LCL, and breakbulk shipments, leveraging Kochi port for seamless global connectivity."
    },
    {
        title: "PRECISION IN<br>AIR FREIGHT.",
        desc: "Every shipment is managed with the highest standards of care and efficiency. As a licensed customs broker and DHL partner, we ensure rapid, secure transit."
    },
    {
        title: "SEAMLESS GROUND<br>TRANSIT.",
        desc: "Reliable logistics solutions designed to meet your business needs. We bridge the gap between major ports and inland destinations across the subcontinent."
    }
];

const slideDuration = 5000; 
let currentSlide = 0;
let slideInterval;

function resetProgressAnimations() {
    indicators.forEach(ind => {
        ind.classList.remove('active');
        const fill = ind.querySelector('.progress-bar-fill');
        fill.style.transition = 'none';
        fill.style.width = '0%';
    });
}

function startProgressAnimation(index) {
    indicators[index].classList.add('active');
    const fill = indicators[index].querySelector('.progress-bar-fill');
    
    setTimeout(() => {
        fill.style.transition = `width ${slideDuration}ms linear`;
        fill.style.width = '100%';
    }, 50);
}

function updateHeroContent() {
    heroContentWrapper.style.opacity = '0';
    setTimeout(() => {
        heroTitle.innerHTML = slideData[currentSlide].title;
        heroDesc.innerHTML = slideData[currentSlide].desc;
        heroContentWrapper.style.opacity = '1';
    }, 300);
}

function changeSlide() {
    slides[currentSlide].classList.remove('active');
    currentSlide = (currentSlide + 1) % slides.length;
    
    resetProgressAnimations();
    slides[currentSlide].classList.add('active');
    
    updateHeroContent();
    startProgressAnimation(currentSlide);
}

function startSlider() {
    startProgressAnimation(currentSlide);
    slideInterval = setInterval(changeSlide, slideDuration);
}

window.goToSlide = function(index) {
    if (currentSlide === index) return;
    
    clearInterval(slideInterval);
    slides[currentSlide].classList.remove('active');
    resetProgressAnimations();
    
    currentSlide = index;
    slides[currentSlide].classList.add('active');
    
    updateHeroContent();
    startSlider();
}

document.addEventListener('DOMContentLoaded', () => {
    startSlider();
});

// --- Mobile Menu Toggle ---
document.querySelector('.menu-toggle').addEventListener('click', function() {
    const navLinks = document.querySelector('.nav-links');
    // Simplified to cleanly toggle the CSS class instead of injecting inline styles
    navLinks.classList.toggle('active');
});

// --- Go To Top Button ---
const goToTopBtn = document.getElementById('goToTopBtn');

window.addEventListener('scroll', () => {
    if (window.scrollY > 400) {
        goToTopBtn.classList.add('show');
    } else {
        goToTopBtn.classList.remove('show');
    }
});

goToTopBtn.addEventListener('click', () => {
    window.scrollTo({
        top: 0,
        behavior: 'smooth'
    });
});

// --- Number Counter Animation ---
const counters = document.querySelectorAll('.count');
const speed = 100; 

const counterObserver = new IntersectionObserver((entries, observer) => {
    entries.forEach(entry => {
        if (entry.isIntersecting) {
            const counter = entry.target;
            const target = +counter.getAttribute('data-target');
            
            const updateCount = () => {
                const count = +counter.innerText;
                const inc = target / speed;
                
                if (count < target) {
                    counter.innerText = Math.ceil(count + inc);
                    setTimeout(updateCount, 15);
                } else {
                    counter.innerText = target;
                }
            };
            
            updateCount();
            observer.unobserve(counter); 
        }
    });
}, { threshold: 0.5 }); 

counters.forEach(counter => {
    counterObserver.observe(counter);
});