$(window).on("load", function () {
    hidePreLoader();
});

showPreLoader();

function showPreLoader() {
    $(".please-wait").css('display', 'block');
    $(".load-wrapper").css('opacity', 1);
}

function hidePreLoader() {
    $(".please-wait").css('display', 'none');
    $(".load-wrapper").css('opacity', 0);
}

$(document).ready(function () {


    // Sidebar toggle behavior
    $('#sidebarCollapse').on('click', function () {
        $('#sidebar, #content').toggleClass('active');
    });

    

})