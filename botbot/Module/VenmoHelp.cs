namespace botbot.Module
{
    public static class VenmoHelp
    {
        public const string VenmoHelpMessage =
@"Venmo for Slack help
Commands:
/venmo balance
    returns your Venmo balance
/venmo last
    returns your last command
/venmo search query
    example: venmo search golf1052
    searches for a Venmo user, search will return up to 10 users
/venmo (audience) pay/charge amount for note to recipients
    example: venmo public charge $10.00 for lunch to testuser phone:5555555555 email:example@example.com
    supports basic arithmetic, does not follow order of operations or support parenthesis
    example: venmo charge 20 + 40 / 3 for brunch to a_user boss phone:5556667777
        this would charge $20 NOT $33.33 to each user in the recipients list
    audience (optional) = public OR friends OR private
        defaults to private if omitted
    pay/charge = pay OR charge
    amount = Venmo amount
    note = Venmo message
    recipients = list of recipients, can specify Venmo username, phone number prefixed with phone: or email prefixed with email: or Venmo id prefixed with user_id:
/venmo alias id alias
    example: venmo alias 4u$3r1d sam
    set an alias for a Venmo username
    id = Venmo username
    alias = the alias for that user, must not contain spaces
/venmo alias list
    list all aliases
/venmo alias delete alias
    example: venmo alias delete sam
    delete an alias
    alias = the alias for that user, must not contain spaces
/venmo pending (incoming OR outgoing)
    returns pending venmo charges, defaults to incoming
    also returns ID for payment completion
/venmo complete accept/reject/cancel number(s)/all
    accept OR reject pending incoming Venmos with the given IDs
    cancel pending outgoing Venmos with the given IDs
/venmo history
    returns up to the last 50 Venmo transactions (payments or transfers)
/venmo schedule {recurrence} {execution date} {payment command}
    example: venmo schedule every beginning of the month pay $10 for Netflix to testuser
        this would pay $10 every 1st of the month to testuser
    note that scheduled Venmos will always execute at 12 PM on the scheduled day unless otherwise specified and will never execute on the current day, so for example if today is Wednesday and you schedule a Venmo for Wednesday it will be scheduled for next week's Wednesday
    recurrence: either 'every' meaning repeated or 'on' OR 'at' meaning one-time
    execution date: supports
        day or tomorrow: the next day
        sunday
        monday
        tuesday
        wednesday
        thursday
        friday
        saturday
        beginning of the month: the first of the month
        end of the month: the last day of the month
        day of the month (1/2/3/.../29/30/31): specified day of the month, if a month doesn't have the specified day of the month the scheduled Venmo will be executed on the last day of the month
        ISO 8601 string: examples: 2020-02-29 or 2020-02-29T18:30. https://en.wikipedia.org/wiki/ISO_8601
    payment command: a valid payment command
/venmo schedule list
    list all scheduled Venmos
/venmo schedule delete ###
    delete the specified scheduled Venmo
/venmo autopay add user is {friend id or alias} {and amount is/=/==/===/</<= ""amount""} {and note is ""note""}
    example: venmo autopay add user is test_user and amount is $4.20 and note is test note
    example: venmo autopay add user is anotheruser and amount <= 10
    example: venmo autopay add user is thirduser and note is another note
    example: venmo autopay add user is fourthuser
    In order to add an autopayment for a Venmo user you must be friends with them or have their Venmo username aliased.
    You can define an amount and/or a note to accept from that Venmo user.
    Once you setup an autopayment, anytime you get a Venmo from that user that matches your defined autopayment you will automatically pay them.
    If you have an autopayment for a Venmo user with an amount or note defined but the charge from that Venmo user doesn't match you'll get a response on why it didn't match.
    There is a built-in cooldown on how often autopayments can be triggered in order to prevent abuse.
/venmo autopay list
    List all defined autopayments
/venmo autopay delete ###
    Delete the specified autopayment
/venmo delete
    Deletes your Venmo authentication information from the database but retains your settings (aliases, schedules).
/venmo delete everything
    Deletes all of your information including your Venmo authentication information and settings (aliases, schedules) and your YNAB authentication information and settings (mappings) from the database.
    This is not reversible.
/venmo auth username password
    *ONLY SEND IN THE VENMO APP DM OR YOU RISK LEAKING YOUR PASSWORD*
    username = Your Venmo login email/username/phone number
    password = Your Venmo password
    Logs you into Venmo. You may receive a 2FA code on your phone. If so send `/venmo otp <CODE>` with the code received.
/venmo otp code
    code = 2FA code received on your phone
    Verifies your 2FA code and logs you into Venmo.
/venmo code code
    No longer supported. Use `/venmo auth username password` instead
/venmo create
    Updates the Venmo Home tab
/venmo help
    this help message";
    }
}
