-- Nexusmods.com
-- I'm only able to get the pics and content atm

-- required output vars
Images = {}
ModName = "";
Downloads = {}
Content = "";

local lines = {}

function escape_pattern(text)
    return text:gsub("([^%w])", "%%%1")
end

function mySplit(inputstr, sep) 
    sep=sep or '%s' 
    local t={}  
    for field,s in string.gmatch(inputstr, "([^"..sep.."]*)("..sep.."?)") do 
        table.insert(t,field)  
        if s=="" then 
            return t 
        end 
    end 
end

function GetPictures()
    local picdata = false;
    for _, line in pairs(lines) do
        if string.find(line, '<ul class=[,"]thumbgallery gallery clearfix[,"]>') then
            picdata = true;
        end
        if picdata == true then
            if string.find(line, '<li class=[,"]thumb [,"] data%-src=[,"]') then
                local dline = mySplit(line, '"')[4];
                dline = mySplit(dline, '"')[1];
                table.insert(Images, dline)
            end
            else if string.find(line, '<div id=[,"]fileinfo[,"] class=[,"]sideitems clearfix[,"]>') then
                picdata = false
            end
        end
    end
end

function GetContent()
    local des = false;
    for _, line in pairs(lines) do
        if string.find(line, '<div class=[,"]container mod_description_container condensed [,"]>') then
            des = true;
        end
        if des == true then
            if string.find(line, '</section>') then
                des =false;
            else
                Content = Content .. line
            end
        end
    end
end

function main()
    print("Nexusmods.com");
    HtmlData = HtmlData:gsub("[\r]", "") -- strip the \r
    for _, line in pairs(split(HtmlData, "\n")) do
        table.insert(lines, line)
    end

    GetPictures();
    GetContent();
end

main();
