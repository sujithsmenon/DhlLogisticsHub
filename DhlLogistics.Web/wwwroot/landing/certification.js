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
    const isDisplayed = window.getComputedStyle(navLinks).display !== 'none';
    const isDarkMode = document.body.classList.contains('dark-mode');
    
    if (isDisplayed && navLinks.style.position === 'absolute') {
        navLinks.style.display = 'none';
    } else {
        navLinks.style.display = 'flex';
        navLinks.style.flexDirection = 'column';
        navLinks.style.position = 'absolute';
        navLinks.style.top = '100%';
        navLinks.style.left = '0';
        navLinks.style.width = '100%';
        navLinks.style.backgroundColor = isDarkMode ? '#082567' : '#FFFFFF';
        navLinks.style.padding = '2rem 5%';
        navLinks.style.boxShadow = '0 10px 20px rgba(0,0,0,0.1)';
        navLinks.style.borderTop = isDarkMode ? '1px solid rgba(255,255,255,0.1)' : '1px solid #EEE';
    }
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