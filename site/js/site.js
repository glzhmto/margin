/* The only script on the site: on wide screens, the phone next to "How Margin works"
   shows the screen for the step you're reading. Without JS every step is fully visible. */
(function () {
  var shot = document.getElementById("how-shot"), steps = [].slice.call(document.querySelectorAll(".step"));
  if (!shot || !steps.length || !("IntersectionObserver" in window)) return;
  document.documentElement.classList.add("js");
  steps.forEach(function (s) { var i = new Image(); i.src = s.getAttribute("data-shot"); });
  function show(step) {
    steps.forEach(function (s) { s.classList.toggle("on", s === step); });
    var src = step.getAttribute("data-shot");
    if (shot.getAttribute("src") !== src) shot.setAttribute("src", src);
  }
  show(steps[0]);
  var io = new IntersectionObserver(function (entries) {
    entries.forEach(function (e) { if (e.isIntersecting) show(e.target); });
  }, { rootMargin: "-45% 0px -45% 0px" });
  steps.forEach(function (s) { io.observe(s); });
})();
