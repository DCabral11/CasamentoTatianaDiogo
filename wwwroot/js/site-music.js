(() => {
    const page = document.getElementById("guest-page");
    function reflectPage() {
        const child = page.contentWindow;
        const url = new URL(child.location.href);
        if (url.origin !== location.origin) return;
        url.searchParams.delete("sitePage");
        // A validation response can be served from a POST-only address.
        if (!child.document.body?.dataset.postResult && location.href !== url.href)
            history.replaceState(history.state, "", url.href);
        document.title = child.document.title;
    }
    function pageLoaded() {
        try {
            reflectPage();
            page.contentWindow.addEventListener("popstate", reflectPage);
            page.contentWindow.addEventListener("hashchange", reflectPage);
        } catch {
            // External destinations cannot be inspected by the wedding site.
        }
    }
    page.addEventListener("load", pageLoaded);
    pageLoaded();

    const placeholder = document.getElementById("music-player");
    const track = placeholder.dataset.track;
    const key = "casamento.spotify.started." + track.split("/").pop();
    window.onSpotifyIframeApiReady = api => {
        api.createController(placeholder, { uri: "spotify:track:" + track.split("/").pop(), height: 80 }, controller => {
            let started = false;
            try { started = sessionStorage.getItem(key) === "1"; } catch { /* Storage is optional. */ }
            const remember = () => {
                started = true;
                try { sessionStorage.setItem(key, "1"); } catch { /* Keep the in-memory flag. */ }
            };
            controller.addListener("playback_started", remember);
            controller.addListener("playback_update", event => {
                if (!started && !event.data.isPaused && !event.data.isBuffering) remember();
            });
            let attempted = false;
            controller.addListener("ready", () => {
                if (attempted || started) return;
                attempted = true;
                controller.play();
            });
        });
    };
    const script = document.createElement("script");
    script.src = "https://open.spotify.com/embed/iframe-api/v1";
    script.async = true;
    document.head.append(script);
})();
