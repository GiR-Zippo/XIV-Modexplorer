-- heliosphere.app

-- required output vars
Images = {};
ModName = "";
Downloads = {};
Content = "";

local lines = {}

-- Internal functions.
function GetJson()
	for _, line in pairs(lines) do
		if string.find(line, '<script type=[,"]application/json') then
			jData = split(line, '[,"]>')[2]
			jData = split(jData, '</script>')[1]
            j = getJsonToken(jData, "body") --get the body
            j = unescape(j) -- unescape it (weird things are going on)
            j = j:sub(2,#j-1) -- remove the " start and end
            j = getJsonToken(j, "data") -- get the data section
            j = getJsonToken(j, "package") -- get the package section
            ModName = getJsonToken(j, "name") -- get the name of the mod
            Content = unescape(getJsonToken(unescape(j), "description")) -- get the description of the mod
		end
	end
end

function GetPictures()
    local des = false;
    for _, line in pairs(lines) do
        if string.find(line, '[,"]><img src=[,"]') then
            pic = split(line, '[,"]><img src=[,"]')
            for _, pline in pairs(pic) do
                if string.find(pline, '/image') then -- I want the images
                    pline = split(pline, '"')[1]
                     table.insert(Images,pline)
                end
            end
            return
        end
    end
end

function GetDownload()
    local des = false;
    for _, line in pairs(lines) do
        if string.find(line, '<a class=[,"]elementor%-button elementor%-button%-link elementor%-size%-lg[,"] href=[,"]') then
            dl = split(line, 'href="')[2]
            table.insert(Downloads, split(dl, '"')[1]);
        end
    end
end

function main()
    print("heliosphere.app");
    HtmlData = HtmlData:gsub("[\r]", "") -- strip the \r
    for _, line in pairs(split(HtmlData, "\n")) do
        table.insert(lines, line)
    end
	GetJson();
    GetPictures();
    --GetDownload();
end

main();
