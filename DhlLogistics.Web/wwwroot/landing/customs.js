// --- Theme Toggle Engine ---
const themeToggle = document.getElementById('theme-toggle');
const body = document.body;

// Load persisted theme
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

// --- Navbar Scroll Behavior ---
const navbar = document.getElementById('navbar');

window.addEventListener('scroll', () => {
    const heroHeight = document.querySelector('.inner-hero').offsetHeight;
    
    if (window.scrollY > (heroHeight - 80)) {
        navbar.classList.add('scrolled');
    } else {
        navbar.classList.remove('scrolled');
    }
});

// --- Mobile Navigation Menu Toggle ---
document.querySelector('.menu-toggle').addEventListener('click', function() {
    const navLinks = document.querySelector('.nav-links');
    // Class toggle mapped directly to CSS logic mapping background variables
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