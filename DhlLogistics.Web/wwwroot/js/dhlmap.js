// wwwroot/js/dhlmap.js
// Leaflet.js map interop for DHL Logistics GPS tracking dashboard
// All functions called via JS.InvokeVoidAsync("DhlMap.xxx", ...)

window.DhlMap = (function () {

    let map = null;
    const markers = {};      // jobId -> L.marker
    const trails  = {};      // jobId -> L.polyline (GPS trail)
    const coords  = {};      // jobId -> [ [lat,lng], ... ]

    // Truck icon for executives
    const truckIcon = (color) => L.divIcon({
        className: '',
        html: `<div style="
            background:${color};
            border:2px solid #fff;
            border-radius:50%;
            width:32px; height:32px;
            display:flex; align-items:center; justify-content:center;
            font-size:16px;
            box-shadow:0 2px 8px rgba(0,0,0,.4);">
            &#x1F69A;
        </div>`,
        iconSize:   [32, 32],
        iconAnchor: [16, 16],
        popupAnchor:[0, -18]
    });

    const colors = [
        '#D40511', '#3b82f6', '#22c55e',
        '#f97316', '#a855f7', '#06b6d4'
    ];
    let colorIndex = 0;
    const jobColors = {};

    function getColor(jobId) {
        if (!jobColors[jobId]) {
            jobColors[jobId] = colors[colorIndex % colors.length];
            colorIndex++;
        }
        return jobColors[jobId];
    }

    return {

        // ── init ──────────────────────────────────────────────────────────────
        // Called once after the map div renders
        init: function (elementId, lat, lng, zoom) {
            if (map) {
                map.remove();
                map = null;
            }

            map = L.map(elementId, {
                center: [lat, lng],
                zoom: zoom,
                zoomControl: true
            });

            // OpenStreetMap tiles (free, no API key needed)
            L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
                attribution: '© OpenStreetMap contributors',
                maxZoom: 19
            }).addTo(map);

            // Add Kochi warehouse marker
            L.marker([9.9312, 76.2673], {
                icon: L.divIcon({
                    className: '',
                    html: `<div style="
                        background:#D40511; color:#fff;
                        border-radius:6px; padding:4px 8px;
                        font-size:11px; font-weight:700;
                        font-family:monospace; white-space:nowrap;
                        box-shadow:0 2px 6px rgba(0,0,0,.4);">
                        📦 Kochi HQ
                    </div>`,
                    iconAnchor: [40, 10]
                })
            })
            .addTo(map)
            .bindPopup('<b>Kochi Warehouse</b><br/>Container Storage Hub');

            console.log('DhlMap initialised on', elementId);
        },

        // ── addMarker ─────────────────────────────────────────────────────────
        // Add a new executive truck marker
        addMarker: function (jobId, lat, lng, popupHtml) {
            if (!map) return;

            const color = getColor(jobId);

            const marker = L.marker([lat, lng], { icon: truckIcon(color) })
                .addTo(map)
                .bindPopup(popupHtml);

            markers[jobId] = marker;

            // Start GPS trail polyline
            coords[jobId]  = [[lat, lng]];
            trails[jobId]  = L.polyline([[lat, lng]], {
                color: color,
                weight: 3,
                opacity: 0.7,
                dashArray: '6 4'
            }).addTo(map);

            console.log('DhlMap: added marker for', jobId, lat, lng);
        },

        // ── moveMarker ────────────────────────────────────────────────────────
        // Move existing marker to new GPS position and extend the trail
        moveMarker: function (jobId, lat, lng, popupHtml) {
            if (!map || !markers[jobId]) {
                // Marker doesn't exist yet — add it
                window.DhlMap.addMarker(jobId, lat, lng, popupHtml);
                return;
            }

            // Animate marker to new position
            markers[jobId].setLatLng([lat, lng]);
            markers[jobId].setPopupContent(popupHtml);

            // Extend the GPS trail
            if (coords[jobId]) {
                coords[jobId].push([lat, lng]);
                // Keep trail to last 50 points to avoid memory issues
                if (coords[jobId].length > 50)
                    coords[jobId].shift();
                trails[jobId].setLatLngs(coords[jobId]);
            }
        },

        // ── removeMarker ──────────────────────────────────────────────────────
        removeMarker: function (jobId) {
            if (markers[jobId]) {
                map.removeLayer(markers[jobId]);
                delete markers[jobId];
            }
            if (trails[jobId]) {
                map.removeLayer(trails[jobId]);
                delete trails[jobId];
                delete coords[jobId];
            }
        },

        // ── panTo ─────────────────────────────────────────────────────────────
        // Pan and zoom map to a specific location
        panTo: function (lat, lng) {
            if (!map) return;
            map.setView([lat, lng], 13, { animate: true });
        },

        // ── fitAll ────────────────────────────────────────────────────────────
        // Fit map bounds to show all active markers
        fitAll: function () {
            if (!map) return;
            const latlngs = Object.values(markers).map(m => m.getLatLng());
            if (latlngs.length === 0) return;
            if (latlngs.length === 1) {
                map.setView(latlngs[0], 13);
            } else {
                map.fitBounds(L.latLngBounds(latlngs).pad(0.2));
            }
        },

        // ── simulateGps (DEV ONLY) ────────────────────────────────────────────
        // Simulates executive GPS movement for testing without a real mobile device
        // Call from browser console: DhlMap.simulateGps()
        simulateGps: function () {
            const jobId = 'JOB-001';
            let lat = 10.9095;
            let lng = 76.9798;

            setInterval(() => {
                lat += (Math.random() - 0.5) * 0.002;
                lng += (Math.random() - 0.4) * 0.002;

                window.DhlMap.moveMarker(
                    jobId, lat, lng,
                    `<b>Rajan Kumar</b><br/>InTransit<br/>32.4 km/h`
                );
                console.log('Simulated GPS:', lat.toFixed(5), lng.toFixed(5));
            }, 3000);
        }
    };

})();
