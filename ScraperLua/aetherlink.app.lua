-- heliosphere.app

-- required output vars
Images = {};
ModName = "";
Downloads = {};
Content = "";
Replaces = {};
ExternalSite = "";

local lines = {}

-- Internal functions.
function GetJson()
	for _, line in pairs(lines) do
		if string.find(line, 'type=[,"]application/json') then
			jData = split(line, '[,"]>{')[2]
			jData = "{" .. split(jData, '</script>')[1]

            j = getJsonToken(jData, "props") --get the body
            j = getJsonToken(j, "pageProps")
            mod = getJsonToken(j, "mod")
            meta = getJsonToken(mod, "meta")
            
            --Modname
            ModName = "[" .. getJsonToken(getJsonToken(meta, "author"), "name"):sub(2,-2) .. "] " .. getJsonToken(getJsonToken(meta, "name"), "long"):sub(2,-2)
            --Description
            Content = getJsonToken(getJsonToken(meta, "description"), "html"):sub(2,-2)
            --Get Replaces
            rep = getJsonToken(meta, "replaces"):sub(2,-2):gsub("Replaces", ""):gsub("'", "")
            rep = rep:gsub("\\", "/"):gsub("/n", ";")
            Replaces = split(rep, ";")

            files = getJsonTokenList(mod, "files")
            for _, content in pairs(files) do
                -- get the images
                if string.find(getJsonToken(content, "type"), "image") then
                    table.insert(Images, getJsonToken(content, "url"):sub(2,-2))
                end
                -- get the downloads
                if string.find(getJsonToken(content, "type"), "file") then
                    str = getJsonToken(content, "url"):sub(2,-2)
                    if string.find(str, "cdn.aetherlink") then
                        table.insert(Downloads, str)
                    end
                    if string.find(str, "patreaon.com") then
                        table.insert(ExternalSite, str)
                    end
                end
            end

		end
	end
end

function main()
    print("aetherlink.app");
    HtmlData = HtmlData:gsub("[\r]", "") -- strip the \r
    for s in HtmlData:gmatch("[^\r\n]+") do
        table.insert(lines, s)
    end
	GetJson();
end

main();
