"use strict";

var connectionId;

var connection = new signalR
    .HubConnectionBuilder()
    .withUrl("/voting-data-broadcast")
    .withAutomaticReconnect()
    .build();



async function start() {
    try {
        await connection.start().then(() => console.log("Connection Started"));
    } catch (err) {
        toastr.error(err);
        setTimeout(() => start(), 5000);
    }
}


connection.onclose(async () => {
    await start();
});

start();

$("body").on("submit", "#form_save_vote", function (ev) {
    ev.preventDefault();
    var data = $(this).serialize();

    $("#Total").val("");
    $.ajax({
        type: 'POST',
        url: '/Votings/SaveVote',
        data: data,
        success: function (result) {
            if (result.success) {
                toastr.success(result.message);
                connection.invoke("BroadcastVotingData", result.message).then(function () { }).catch(function (err) { return console.log(err); })
            } else {
                toastr.error(result.message);
            }
        },
        error: function () {
            toastr.error('Failed to receive the Data');
            console.log('Failed ');
        }
    })
})

connection.on("OnbroadcastVotingDataListener", function (message) {
    toastr.success(message);
});
